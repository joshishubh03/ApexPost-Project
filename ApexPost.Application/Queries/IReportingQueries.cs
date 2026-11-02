namespace ApexPost.Application.Queries;

/// <summary>
/// Query side of the application (CQS). Reports and dashboard reads go through this service, which uses
/// AsNoTracking projections or stored procedures. Writes never use it. Both sides share the same ApexPostDb
/// database - no separate read database is required.
/// </summary>
public interface IReportingQueries
{
    Task<IReadOnlyList<DailyBranchSalesDto>> GetDailyBranchSalesAsync(string branchCode, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowStockDto>> GetLowStockAsync(string? branchCode, int threshold, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShippingVolumeDto>> GetShippingVolumeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MoneyOrderBranchSummaryDto>> GetMoneyOrdersByBranchAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchWithoutSalesDto>> GetBranchesWithoutSalesAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<DashboardSummaryDto> GetDashboardAsync(Guid? branchId, CancellationToken cancellationToken = default);
}

/// <summary>Command side: set-based price revision (ExecuteUpdateAsync) recorded with an audit entry.</summary>
public interface IPriceRevisionService
{
    /// <summary>Changes every active product price in <paramref name="category"/> by <paramref name="percentChange"/> percent. Returns the number of rows updated.</summary>
    Task<int> RevisePricesAsync(string category, decimal percentChange, Guid actorId, string correlationId, CancellationToken cancellationToken = default);
}
