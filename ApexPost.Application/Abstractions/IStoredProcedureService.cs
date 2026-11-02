using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Parameterized calls to the stored procedures in sql/stored-procedures.sql (created by migration).
/// Implementations use ExecuteSql/SqlQuery with interpolated parameters only - never string concatenation.
/// </summary>
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

    Task<StockAdjustmentResult> AdjustStockAsync(
        string branchCode,
        string sku,
        int delta,
        string reason,
        string changedBy,
        CancellationToken cancellationToken = default);
}
