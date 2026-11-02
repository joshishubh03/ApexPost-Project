using ApexPost.Application.Abstractions;
using ApexPost.Application.Queries;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Queries;

/// <summary>
/// Query service (read side). Uses AsNoTracking projections straight into DTOs, or the reporting stored
/// procedures. Direct DbContext projections are allowed here (and only here) because reads never need a
/// domain aggregate - see ADR 0044. Every query below is a single SQL statement (no N+1).
/// </summary>
public sealed class EfReportingQueries(ApexPostDbContext context, IStoredProcedureService storedProcedures) : IReportingQueries
{
    private static readonly TransactionStatus[] SettledStatuses = [TransactionStatus.Paid, TransactionStatus.Completed];

    public async Task<IReadOnlyList<DailyBranchSalesDto>> GetDailyBranchSalesAsync(
        string branchCode,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var rows = await storedProcedures.GetDailyBranchSalesAsync(branchCode, date, cancellationToken);
        return rows.Select(row => new DailyBranchSalesDto(row.BranchCode, row.SaleDate, row.TransactionCount, row.RetailRevenue, row.ServiceRevenue)).ToArray();
    }

    public async Task<IReadOnlyList<LowStockDto>> GetLowStockAsync(
        string? branchCode,
        int threshold,
        CancellationToken cancellationToken = default)
    {
        var rows = await storedProcedures.GetLowStockReportAsync(branchCode, threshold, cancellationToken);
        return rows.Select(row => new LowStockDto(row.BranchCode, row.Sku, row.ProductName, row.QuantityOnHand, row.ReorderLevel, row.Shortfall)).ToArray();
    }

    public async Task<IReadOnlyList<ShippingVolumeDto>> GetShippingVolumeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await ShippingVolumeQuery(fromDate, toDate).ToListAsync(cancellationToken);
        return rows.Select(row => new ShippingVolumeDto(row.ZoneCode, row.ZoneName, row.ServiceLevel.ToString(), row.ShipmentCount, row.ShippingRevenue)).ToArray();
    }

    public async Task<IReadOnlyList<MoneyOrderBranchSummaryDto>> GetMoneyOrdersByBranchAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await MoneyOrdersByBranchQuery(fromDate, toDate).ToListAsync(cancellationToken);
        return rows.Select(row => new MoneyOrderBranchSummaryDto(
            row.BranchCode.Value, row.BranchName, row.Issued, row.Refunded, row.Value, row.Passed, row.Failed)).ToArray();
    }

    public async Task<IReadOnlyList<BranchWithoutSalesDto>> GetBranchesWithoutSalesAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var rows = await BranchesWithoutSalesQuery(date).ToListAsync(cancellationToken);
        return rows.Select(row => new BranchWithoutSalesDto(row.Code.Value, row.Name, row.Region)).ToArray();
    }

    public async Task<DashboardSummaryDto> GetDashboardAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = UtcDay(DateOnly.FromDateTime(DateTime.UtcNow));
        var transactions = context.CounterTransactions.AsNoTracking()
            .Where(row => row.OccurredAt >= fromUtc && row.OccurredAt < toUtc && SettledStatuses.Contains(row.Status));
        var stock = context.BranchStock.AsNoTracking().Where(row => row.QuantityOnHand <= row.ReorderLevel);
        var moneyOrders = context.MoneyOrders.AsNoTracking().Where(row => row.OccurredAt >= fromUtc && row.OccurredAt < toUtc);
        if (branchId is { } branch)
        {
            transactions = transactions.Where(row => row.BranchId == branch);
            stock = stock.Where(row => row.BranchId == branch);
            moneyOrders = moneyOrders.Where(row => row.BranchId == branch);
        }

        // Sequential awaits: one DbContext never runs two commands at once.
        var branchCount = await context.Branches.AsNoTracking().CountAsync(cancellationToken);
        var productCount = await context.Products.AsNoTracking().CountAsync(row => row.IsActive, cancellationToken);
        var todayCount = await transactions.CountAsync(cancellationToken);
        var todayRevenue = await transactions.SumAsync(row => (decimal?)row.TotalAmount, cancellationToken) ?? 0m;
        var lowStock = await stock.CountAsync(cancellationToken);
        var todayMoneyOrders = await moneyOrders.CountAsync(cancellationToken);
        return new DashboardSummaryDto(branchCount, productCount, todayCount, todayRevenue, lowStock, todayMoneyOrders);
    }

    /// <summary>
    /// Captures the SQL EF Core generates for the main report queries (ToQueryString) so it can be reviewed
    /// in PERFORMANCE.md without running them.
    /// </summary>
    public IReadOnlyList<GeneratedSqlSample> CaptureGeneratedSql(DateOnly fromDate, DateOnly toDate)
    {
        var branchId = Guid.Empty; // captured variable -> sent as a SQL parameter, never inlined (no database round-trip here)
        return
    [
        new("Shipping volume by zone and service level", ShippingVolumeQuery(fromDate, toDate).ToQueryString()),
        new("Money orders and identity verifications per branch", MoneyOrdersByBranchQuery(fromDate, toDate).ToQueryString()),
        new("Branches with no sales on a day (NOT EXISTS / anti-join)", BranchesWithoutSalesQuery(toDate).ToQueryString()),
        new("Recent counter transactions for a branch (split query, first statement)",
            context.CounterTransactions.AsNoTracking()
                .Include(row => row.Lines)
                .Include(row => row.Payment)
                .Include(row => row.Shipment)
                .AsSplitQuery()
                .Where(row => row.BranchId == branchId)
                .OrderByDescending(row => row.OccurredAt)
                .Take(20)
                .ToQueryString())
    ];
    }

    private IQueryable<ShippingVolumeRow> ShippingVolumeQuery(DateOnly fromDate, DateOnly toDate)
    {
        var (fromUtc, _) = UtcDay(fromDate);
        var (_, toUtc) = UtcDay(toDate);
        return
            from shipment in context.Shipments.AsNoTracking()
            join transaction in context.CounterTransactions.AsNoTracking() on shipment.CounterTransactionId equals transaction.Id
            join rate in context.ShippingRates.AsNoTracking() on shipment.ShippingRateId equals rate.Id
            join zone in context.ShippingZones.AsNoTracking() on rate.ZoneId equals zone.Id
            where transaction.OccurredAt >= fromUtc && transaction.OccurredAt < toUtc
                  && shipment.Status == ShipmentStatus.Booked
            group transaction by new { ZoneCode = zone.Code, ZoneName = zone.Name, rate.ServiceLevel } into grouped
            orderby grouped.Key.ZoneCode, grouped.Key.ServiceLevel
            select new ShippingVolumeRow(
                grouped.Key.ZoneCode,
                grouped.Key.ZoneName,
                grouped.Key.ServiceLevel,
                grouped.Count(),
                grouped.Sum(item => item.ShippingCharge));
    }

    private IQueryable<MoneyOrderSummaryRow> MoneyOrdersByBranchQuery(DateOnly fromDate, DateOnly toDate)
    {
        var (fromUtc, _) = UtcDay(fromDate);
        var (_, toUtc) = UtcDay(toDate);
        return context.Branches.AsNoTracking()
            .OrderBy(branch => branch.Code)
            .Select(branch => new MoneyOrderSummaryRow(
                branch.Code,
                branch.Name,
                context.MoneyOrders.Count(order => order.BranchId == branch.Id && order.OccurredAt >= fromUtc && order.OccurredAt < toUtc && order.Status == MoneyOrderStatus.Issued),
                context.MoneyOrders.Count(order => order.BranchId == branch.Id && order.OccurredAt >= fromUtc && order.OccurredAt < toUtc && order.Status == MoneyOrderStatus.Refunded),
                context.MoneyOrders.Where(order => order.BranchId == branch.Id && order.OccurredAt >= fromUtc && order.OccurredAt < toUtc)
                    .Sum(order => (decimal?)order.Amount) ?? 0m,
                context.IdentityVerifications.Count(check => check.BranchId == branch.Id && check.OccurredAt >= fromUtc && check.OccurredAt < toUtc && check.Status == IdentityVerificationStatus.Verified),
                context.IdentityVerifications.Count(check => check.BranchId == branch.Id && check.OccurredAt >= fromUtc && check.OccurredAt < toUtc && check.Status == IdentityVerificationStatus.Failed)));
    }

    private IQueryable<BranchWithoutSalesRow> BranchesWithoutSalesQuery(DateOnly date)
    {
        var (fromUtc, toUtc) = UtcDay(date);
        return context.Branches.AsNoTracking()
            .Where(branch => !context.CounterTransactions.Any(transaction =>
                transaction.BranchId == branch.Id &&
                transaction.OccurredAt >= fromUtc && transaction.OccurredAt < toUtc &&
                SettledStatuses.Contains(transaction.Status)))
            .OrderBy(branch => branch.Code)
            .Select(branch => new BranchWithoutSalesRow(branch.Code, branch.Name, branch.Region));
    }

    /// <summary>SARGable day window: OccurredAt &gt;= start AND OccurredAt &lt; next day (never CAST(column AS date)).</summary>
    private static (DateTimeOffset From, DateTimeOffset To) UtcDay(DateOnly date)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return (from, from.AddDays(1));
    }

    private sealed record BranchWithoutSalesRow(BranchCode Code, string Name, string Region);

    private sealed record ShippingVolumeRow(string ZoneCode, string ZoneName, ShippingServiceLevel ServiceLevel, int ShipmentCount, decimal ShippingRevenue);

    private sealed record MoneyOrderSummaryRow(
        Domain.ValueObjects.BranchCode BranchCode,
        string BranchName,
        int Issued,
        int Refunded,
        decimal Value,
        int Passed,
        int Failed);
}
