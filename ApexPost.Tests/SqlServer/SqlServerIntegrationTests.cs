using System.Data.Common;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Queries;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Entities;
using ApexPost.Infrastructure.Persistence.Queries;
using ApexPost.Infrastructure.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ApexPost.Tests.SqlServer;

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerIntegrationTests(SqlServerTestDatabase database, ITestOutputHelper output)
{
    [Fact]
    public async Task Seeded_database_meets_the_assignment_volumes()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();

        Assert.True(await context.Branches.CountAsync() >= 5);
        Assert.True(await context.Staff.CountAsync() >= 20);
        Assert.True(await context.Products.CountAsync() >= 30);
        Assert.True(await context.ShippingZones.CountAsync() >= 10);
        Assert.True(await context.ShippingRates.CountAsync() >= 10);
        Assert.True(await context.CounterTransactions.CountAsync() >= DemoDataSeeder.HistoricalTransactionCount);
        Assert.True(await context.Shipments.CountAsync() > 0);
    }

    [Fact]
    public async Task Repository_maps_the_transaction_aggregate_and_value_objects_round_trip()
    {
        var data = await TestData.LoadAsync(database);
        var rate = data.Rate;

        Guid transactionId;
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<CounterTransactionService>();
            var sale = await service.CompleteSaleAsync(
                data.BranchId, data.ClerkId, new Dictionary<Guid, int> { [data.ProductId] = 2 }, PaymentMethod.Card,
                rate.Id, ShipmentType.Parcel, 1.25m, "Asha Rao", "12 MG Road, Pune 411001", 20m, 15m, 10m,
                data.TerminalId, null, null, null, null, null, "it-roundtrip");
            transactionId = sale.Id;
        }

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>();
            var loaded = await repository.GetByIdAsync(transactionId);

            Assert.NotNull(loaded);
            var line = Assert.Single(loaded.Lines);
            Assert.Equal(data.ProductSku, line.Sku);
            Assert.Equal(2, line.Quantity);
            Assert.Equal(PaymentMethod.Card, loaded.PaymentMethod);
            Assert.Equal(1.25m, loaded.ShippingWeightKilograms);
            Assert.Equal(rate.ServiceLevel, loaded.ShippingServiceLevel);
            Assert.Equal(data.ZoneName, loaded.ShippingZoneName);
            Assert.Equal("Asha Rao", loaded.ShippingRecipientName);
            Assert.Equal(20m, loaded.ParcelLengthCentimeters);

            var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
            var shipment = await context.Shipments.AsNoTracking().SingleAsync(row => row.CounterTransactionId == transactionId);
            Assert.Equal(ShipmentStatus.Booked, shipment.Status);
            var createdBy = await context.CounterTransactions.AsNoTracking()
                .Where(row => row.Id == transactionId)
                .Select(row => EF.Property<Guid?>(row, AuditShadowProperties.CreatedBy))
                .SingleAsync();
            Assert.Equal(data.ClerkId, createdBy);
        }
    }

    [Fact]
    public async Task Failed_sale_rolls_back_stock_and_transaction_rows_together()
    {
        var data = await TestData.LoadAsync(database);
        var (stockBefore, transactionsBefore, ordersBefore) = await CountsAsync(data);

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<CounterTransactionService>();
            // Stock line + money order above the ID threshold with a document the verifier rejects:
            // the stock decrement and the transaction were already staged when the money order failed.
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteSaleAsync(
                data.BranchId, data.ClerkId, new Dictionary<Guid, int> { [data.ProductId] = 1 }, PaymentMethod.Cash,
                null, null, null, null, null, null, null, null, data.TerminalId,
                20_000m, "Sender", "Recipient", IdentityDocumentType.Passport, "REJECT-1234", "it-rollback"));
        }

        var (stockAfter, transactionsAfter, ordersAfter) = await CountsAsync(data);
        Assert.Equal(stockBefore, stockAfter);
        Assert.Equal(transactionsBefore, transactionsAfter);
        Assert.Equal(ordersBefore, ordersAfter);
    }

    [Fact]
    public async Task Two_clerks_selling_the_last_unit_cannot_drive_stock_negative()
    {
        var data = await TestData.LoadAsync(database, productOffset: 3);
        await SetStockAsync(data, 1);

        var interceptor = new CompetingSaleInterceptor();
        await using var racingServices = SqlServerTestDatabase.CreateServices(
            database.ConnectionString,
            services => services.AddSingleton<ISaveChangesInterceptor>(interceptor));
        interceptor.CompetingSale = async () =>
        {
            // Clerk B sells the same last unit and commits while clerk A is between read and commit.
            await using var scope = database.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CounterTransactionService>().CompleteSaleAsync(
                data.BranchId, data.SecondClerkId, new Dictionary<Guid, int> { [data.ProductId] = 1 }, PaymentMethod.Cash,
                null, null, null, null, null, null, null, null, data.TerminalId,
                null, null, null, null, null, "it-clerk-b");
        };

        await using (var scope = racingServices.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<CounterTransactionService>();
            var conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.CompleteSaleAsync(
                data.BranchId, data.ClerkId, new Dictionary<Guid, int> { [data.ProductId] = 1 }, PaymentMethod.Cash,
                null, null, null, null, null, null, null, null, data.TerminalId,
                null, null, null, null, null, "it-clerk-a"));
            Assert.Contains("refresh", conflict.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(interceptor.Fired);
        Assert.Equal(0, (await CountsAsync(data)).Stock);
    }

    [Fact]
    public async Task Stored_procedures_return_the_expected_results()
    {
        var data = await TestData.LoadAsync(database);
        await using var scope = database.Services.CreateAsyncScope();
        var procedures = scope.ServiceProvider.GetRequiredService<IStoredProcedureService>();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();

        // usp_DailyBranchSales: compare with the same totals computed independently for a day that has sales.
        var lastSale = await context.CounterTransactions.AsNoTracking()
            .Where(row => row.BranchId == data.BranchId && row.Status == TransactionStatus.Paid && row.OccurredAt < DateTimeOffset.UtcNow.AddDays(-1))
            .OrderByDescending(row => row.OccurredAt)
            .Select(row => row.OccurredAt)
            .FirstAsync();
        var day = DateOnly.FromDateTime(lastSale.UtcDateTime);
        var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        string[] serviceCategories = ["Postage", "Shipping", "Money Order"];
        var sales = await context.CounterTransactions.AsNoTracking()
            .Where(row => row.BranchId == data.BranchId && row.OccurredAt >= dayStart && row.OccurredAt < dayEnd &&
                          (row.Status == TransactionStatus.Paid || row.Status == TransactionStatus.Completed))
            .Select(row => new { row.Id, Extras = row.ShippingCharge + row.ShippingTaxAmount + row.MoneyOrderAmount + row.MoneyOrderFee })
            .ToListAsync();
        var saleIds = sales.Select(row => row.Id).ToArray();
        var lines = await (
                from line in context.TransactionLines.AsNoTracking()
                join product in context.Products.AsNoTracking() on line.ProductId equals product.Id
                where saleIds.Contains(line.CounterTransactionId)
                select new { Amount = line.LineTotal + line.TaxAmount, product.Category })
            .ToListAsync();
        var expectedRetail = lines.Where(line => !serviceCategories.Contains(line.Category)).Sum(line => line.Amount);
        var expectedService = lines.Where(line => serviceCategories.Contains(line.Category)).Sum(line => line.Amount) + sales.Sum(row => row.Extras);

        var daily = Assert.Single(await procedures.GetDailyBranchSalesAsync(data.BranchCode, day));
        Assert.True(daily.TransactionCount > 0);
        Assert.Equal(sales.Count, daily.TransactionCount);
        Assert.Equal(expectedRetail, daily.RetailRevenue);
        Assert.Equal(expectedService, daily.ServiceRevenue);

        // usp_LowStockReport: optional branch parameter, threshold respected, seeded low-stock rows returned.
        var allLow = await procedures.GetLowStockReportAsync(null, 10);
        Assert.NotEmpty(allLow);
        Assert.All(allLow, row => Assert.True(row.QuantityOnHand <= 10));
        var branchLow = await procedures.GetLowStockReportAsync(data.BranchCode, 10);
        Assert.All(branchLow, row => Assert.Equal(data.BranchCode, row.BranchCode));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => procedures.GetLowStockReportAsync("ZZ-404", 10));

        // usp_AdjustStock: transactional, writes the history row, refuses negative stock.
        var adjustmentsBefore = await context.StockAdjustments.CountAsync(row => row.BranchId == data.BranchId && row.ProductId == data.ProductId);
        var stockBefore = (await CountsAsync(data)).Stock;
        var added = await procedures.AdjustStockAsync(data.BranchCode, data.ProductSku, 5, "Integration test delivery", data.ManagerEmail);
        Assert.Equal(stockBefore + 5, added.QuantityOnHand);
        Assert.Equal(adjustmentsBefore + 1, await context.StockAdjustments.CountAsync(row => row.BranchId == data.BranchId && row.ProductId == data.ProductId));

        var negative = await Assert.ThrowsAsync<SqlException>(() =>
            procedures.AdjustStockAsync(data.BranchCode, data.ProductSku, -(stockBefore + 1000), "Too much", data.ManagerEmail));
        Assert.Equal(50012, negative.Number);
        Assert.Equal(stockBefore + 5, (await CountsAsync(data)).Stock);
    }

    [Fact]
    public async Task Price_revision_is_set_based_and_audited_in_one_transaction()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        var before = await context.Products.AsNoTracking()
            .Where(row => row.Category == "Stationery" && row.IsActive)
            .ToDictionaryAsync(row => row.Id, row => row.Price);

        var updated = await scope.ServiceProvider.GetRequiredService<IPriceRevisionService>()
            .RevisePricesAsync("Stationery", 10m, (await TestData.LoadAsync(database)).ManagerId, "it-price-revision");

        Assert.Equal(before.Count, updated);
        var after = await context.Products.AsNoTracking()
            .Where(row => row.Category == "Stationery" && row.IsActive)
            .ToDictionaryAsync(row => row.Id, row => row.Price);
        Assert.All(before, pair => Assert.Equal(Math.Round(pair.Value * 1.1m, 2), after[pair.Key]));
        Assert.True(await context.AuditEntries.AnyAsync(row => row.CorrelationId == "it-price-revision" && row.Action == "ProductPriceRevision"));
    }

    [Fact]
    public async Task Reading_recent_transactions_uses_a_fixed_number_of_commands_not_one_per_row()
    {
        var data = await TestData.LoadAsync(database);
        var counter = new CommandCounter();
        await using var context = new ApexPostDbContext(new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(database.ConnectionString)
            .AddInterceptors(counter)
            .Options);
        var repository = new EfCounterTransactionRepository(context);

        var recent = await repository.GetRecentForBranchAsync(data.BranchId, 20);

        Assert.Equal(20, recent.Count);
        output.WriteLine($"Commands executed for 20 transactions with lines, payment and shipment: {counter.Count}");
        // Split query: transactions + lines + payments + shipments, then one shipping lookup = 5 commands,
        // regardless of how many transactions are returned. N+1 would be 20+.
        Assert.InRange(counter.Count, 1, 5);
    }

    [Fact]
    public async Task Generated_sql_for_the_main_queries_is_captured_with_ToQueryString()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<EfReportingQueries>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var samples = queries.CaptureGeneratedSql(today.AddDays(-30), today);

        Assert.True(samples.Count >= 3);
        Assert.Contains(samples, sample => sample.Sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(samples, sample => sample.Sql.Contains("NOT EXISTS", StringComparison.OrdinalIgnoreCase));
        var document = string.Join(Environment.NewLine + Environment.NewLine, samples.Select(sample =>
            $"### {sample.Name}{Environment.NewLine}{Environment.NewLine}```sql{Environment.NewLine}{sample.Sql}{Environment.NewLine}```"));
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "generated-sql.md"), document);
        output.WriteLine(document);
    }

    private async Task<(int Stock, int Transactions, int MoneyOrders)> CountsAsync(TestData data)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        var stock = await context.BranchStock.AsNoTracking()
            .Where(row => row.BranchId == data.BranchId && row.ProductId == data.ProductId)
            .Select(row => row.QuantityOnHand)
            .SingleAsync();
        return (stock,
            await context.CounterTransactions.CountAsync(),
            await context.MoneyOrders.CountAsync());
    }

    private async Task SetStockAsync(TestData data, int quantity)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        await context.BranchStock
            .Where(row => row.BranchId == data.BranchId && row.ProductId == data.ProductId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.QuantityOnHand, quantity));
    }

    /// <summary>Runs clerk B's sale the first time clerk A's unit of work is about to save a stock change.</summary>
    private sealed class CompetingSaleInterceptor : SaveChangesInterceptor
    {
        public Func<Task>? CompetingSale { get; set; }
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var changesStock = eventData.Context?.ChangeTracker.Entries<BranchStockRow>()
                .Any(entry => entry.State == EntityState.Modified) == true;
            if (!Fired && changesStock && CompetingSale is not null)
            {
                Fired = true;
                await CompetingSale();
            }

            return result;
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int count;
        public int Count => count;
        public void Reset() => Interlocked.Exchange(ref count, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
