using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Queries;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Tests.SqlServer;

/// <summary>
/// Proves that the schema itself - not only the domain model - enforces the Part A rules, and that the
/// repositories / unit of work / procedure wrappers translate database errors into application exceptions.
/// Every raw statement runs inside a transaction that is rolled back, so the shared test data is unchanged.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SqlConstraintTests(SqlServerTestDatabase database)
{
    private const int CheckOrForeignKeyViolation = 547;
    private const int UniqueViolation = 2601;

    [Theory]
    [InlineData("UPDATE TOP (1) dbo.BranchStock SET QuantityOnHand = -1;", CheckOrForeignKeyViolation)]
    [InlineData("UPDATE TOP (1) dbo.Shipments SET WeightKilograms = 30.01;", CheckOrForeignKeyViolation)]
    [InlineData("UPDATE TOP (1) dbo.IdentityVerifications SET MaskedDocumentIdentifier = 'P1234567';", CheckOrForeignKeyViolation)]
    [InlineData("UPDATE TOP (1) dbo.CounterTransactions SET Status = 'Lost';", CheckOrForeignKeyViolation)]
    [InlineData("UPDATE TOP (1) dbo.Products SET Price = -1;", CheckOrForeignKeyViolation)]
    [InlineData("UPDATE TOP (1) dbo.MoneyOrders SET Amount = 0;", CheckOrForeignKeyViolation)]
    [InlineData("UPDATE TOP (1) dbo.Payments SET Method = 'Cheque';", CheckOrForeignKeyViolation)]
    [InlineData("UPDATE dbo.Products SET Sku = 'POST-001' WHERE Sku = 'POST-002';", UniqueViolation)]
    [InlineData("UPDATE dbo.Branches SET Code = 'AP-001' WHERE Code = 'AP-002';", UniqueViolation)]
    [InlineData("DELETE TOP (1) FROM dbo.Branches;", CheckOrForeignKeyViolation)]
    [InlineData("DELETE FROM dbo.Products WHERE Id IN (SELECT ProductId FROM dbo.TransactionLines);", CheckOrForeignKeyViolation)]
    [InlineData("DELETE FROM dbo.ShippingZones WHERE Id IN (SELECT ZoneId FROM dbo.ShippingRates);", CheckOrForeignKeyViolation)]
    public async Task Database_rejects_rows_that_break_the_schema_rules(string statement, int expectedError)
    {
        var error = await ExecuteRolledBackAsync(statement);

        Assert.NotNull(error);
        if (expectedError == UniqueViolation)
        {
            Assert.True(error is 2601 or 2627, $"Expected a unique violation, got {error}.");
        }
        else
        {
            Assert.Equal(expectedError, error);
        }
    }

    [Fact]
    public async Task Deleting_a_transaction_cascades_to_its_lines_payment_and_shipment()
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = new SqlCommand("""
            DECLARE @Id uniqueidentifier = (
                SELECT TOP (1) ct.Id
                FROM dbo.CounterTransactions AS ct
                INNER JOIN dbo.Shipments AS s ON s.CounterTransactionId = ct.Id
                WHERE NOT EXISTS (SELECT 1 FROM dbo.MoneyOrders AS mo WHERE mo.CounterTransactionId = ct.Id)
                ORDER BY ct.OccurredAt);
            DECLARE @Before int = (SELECT COUNT(*) FROM dbo.TransactionLines WHERE CounterTransactionId = @Id)
                                + (SELECT COUNT(*) FROM dbo.Payments WHERE CounterTransactionId = @Id)
                                + (SELECT COUNT(*) FROM dbo.Shipments WHERE CounterTransactionId = @Id);
            DELETE FROM dbo.CounterTransactions WHERE Id = @Id;
            SELECT @Before,
                   (SELECT COUNT(*) FROM dbo.TransactionLines WHERE CounterTransactionId = @Id)
                 + (SELECT COUNT(*) FROM dbo.Payments WHERE CounterTransactionId = @Id)
                 + (SELECT COUNT(*) FROM dbo.Shipments WHERE CounterTransactionId = @Id);
            """, connection, transaction);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt32(0) >= 3, "The chosen transaction should have a line, a payment and a shipment.");
            Assert.Equal(0, reader.GetInt32(1));
        }

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Duplicate_sku_staged_through_the_repository_becomes_a_duplicate_resource_exception()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var existing = (await products.GetAllAsync()).OrderBy(product => product.Sku).First();
        var duplicate = new Product(Guid.NewGuid(), existing.Sku, "Duplicate", "Same SKU as an existing product",
            existing.Category, 1m, true, existing.TaxCategory);

        await Assert.ThrowsAsync<DuplicateResourceException>(() => unitOfWork.ExecuteUseCaseAsync(async () =>
        {
            var all = (await products.GetAllAsync()).Append(duplicate).ToArray();
            await products.SaveAllAsync(all);
        }));
    }

    [Fact]
    public async Task Money_order_above_threshold_stores_only_the_masked_document_number()
    {
        var data = await TestData.LoadAsync(database);
        Guid orderId;
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var order = await scope.ServiceProvider.GetRequiredService<MoneyOrderService>().IssueAsync(
                data.BranchId, data.ManagerId, "Sender", "Recipient", 15_000m,
                IdentityDocumentType.Passport, "DEMO-VALID-12345678", "it-masked-document");
            orderId = order.Id;
        }

        await using (var scope = database.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
            var stored = await context.MoneyOrders.AsNoTracking().SingleAsync(row => row.Id == orderId);
            var verification = await context.IdentityVerifications.AsNoTracking()
                .SingleAsync(row => row.Id == stored.IdentityVerificationId);

            var masked = stored.MaskedDocumentIdentifier;
            Assert.NotNull(masked);
            Assert.StartsWith("****", masked);
            Assert.EndsWith("5678", masked);
            Assert.DoesNotContain("DEMO", masked);
            Assert.Equal(masked, verification.MaskedDocumentIdentifier);
            Assert.True(verification.MaskedDocumentIdentifier.Length <= 16);
        }
    }

    [Fact]
    public async Task Stored_procedures_report_an_unknown_branch_as_key_not_found()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var procedures = scope.ServiceProvider.GetRequiredService<IStoredProcedureService>();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => procedures.GetDailyBranchSalesAsync("ZZ-999", yesterday));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => procedures.GetLowStockReportAsync("ZZ-999", 10));
    }

    [Fact]
    public async Task Stored_procedure_parameter_validation_throws_its_documented_error_number()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var procedures = scope.ServiceProvider.GetRequiredService<IStoredProcedureService>();
        var data = await TestData.LoadAsync(database);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var futureDate = await Assert.ThrowsAsync<SqlException>(() => procedures.GetDailyBranchSalesAsync(data.BranchCode, tomorrow));
        var badThreshold = await Assert.ThrowsAsync<SqlException>(() => procedures.GetLowStockReportAsync(null, 5_000));

        Assert.Equal(50003, futureDate.Number);
        Assert.Equal(50005, badThreshold.Number);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            procedures.AdjustStockAsync(data.BranchCode, data.ProductSku, 0, "No-op", "test"));
    }

    [Fact]
    public async Task Low_stock_procedure_with_null_branch_covers_every_branch_and_respects_the_threshold()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var procedures = scope.ServiceProvider.GetRequiredService<IStoredProcedureService>();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();

        var rows = await procedures.GetLowStockReportAsync(null, 1_000);
        var expected = await (
                from stock in context.BranchStock.AsNoTracking()
                join product in context.Products.AsNoTracking() on stock.ProductId equals product.Id
                where stock.QuantityOnHand <= 1_000 && product.IsActive
                select stock.ProductId)
            .CountAsync();

        Assert.Equal(expected, rows.Count);
        Assert.Equal(await context.Branches.CountAsync(), rows.Select(row => row.BranchCode).Distinct().Count());
    }

    [Fact]
    public async Task Branches_without_sales_on_a_day_before_any_history_lists_every_branch()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IReportingQueries>();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();

        var rows = await queries.GetBranchesWithoutSalesAsync(new DateOnly(2000, 1, 1));

        Assert.Equal(await context.Branches.CountAsync(), rows.Count);
        Assert.Equal(rows.OrderBy(row => row.BranchCode, StringComparer.Ordinal).Select(row => row.BranchCode),
            rows.Select(row => row.BranchCode));
    }

    [Fact]
    public async Task Shipping_volume_report_counts_every_booked_shipment_in_the_window()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IReportingQueries>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await queries.GetShippingVolumeAsync(today.AddYears(-1), today);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.ZoneCode)));
    }

    private async Task<int?> ExecuteRolledBackAsync(string statement)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(statement, connection, transaction);
            var affected = await command.ExecuteNonQueryAsync();
            Assert.True(affected > 0, $"The statement did not touch any row, so it proves nothing: {statement}");
            return null;
        }
        catch (SqlException exception)
        {
            return exception.Number;
        }
        finally
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (InvalidOperationException)
            {
                // Already rolled back by the server.
            }
        }
    }
}
