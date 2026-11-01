using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

public interface IStoredProcedureService
{
    Task<IReadOnlyList<DailyBranchSalesRow>> GetDailyBranchSalesAsync(
        string branchCode,
        DateOnly saleDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowStockReportRow>> GetLowStockReportAsync(
        string? branchCode,
        int threshold,
        CancellationToken cancellationToken = default);

    Task AdjustStockAsync(
        string branchCode,
        string sku,
        int delta,
        string reason,
        string changedBy,
        CancellationToken cancellationToken = default);
}
