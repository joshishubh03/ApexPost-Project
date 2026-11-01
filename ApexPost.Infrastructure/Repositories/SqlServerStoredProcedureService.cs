using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

public sealed class SqlServerStoredProcedureService(ApexPostDbContext dbContext) : IStoredProcedureService
{
    public async Task<IReadOnlyList<DailyBranchSalesRow>> GetDailyBranchSalesAsync(
        string branchCode,
        DateOnly saleDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        return await dbContext.Database
            .SqlQuery<DailyBranchSalesRow>($"EXEC dbo.usp_DailyBranchSales @BranchCode = {branchCode}, @Date = {saleDate}")
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LowStockReportRow>> GetLowStockReportAsync(
        string? branchCode,
        int threshold,
        CancellationToken cancellationToken = default)
    {
        if (threshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold cannot be negative.");
        }

        object branchCodeParameter = string.IsNullOrWhiteSpace(branchCode) ? DBNull.Value : branchCode;

        return await dbContext.Database
            .SqlQuery<LowStockReportRow>($"EXEC dbo.usp_LowStockReport @BranchCode = {branchCodeParameter}, @Threshold = {threshold}")
            .ToListAsync(cancellationToken);
    }

    public async Task AdjustStockAsync(
        string branchCode,
        string sku,
        int delta,
        string reason,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);

        if (delta == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Stock delta cannot be zero.");
        }

        await dbContext.Database.ExecuteSqlAsync(
            $"EXEC dbo.usp_AdjustStock @BranchCode = {branchCode}, @Sku = {sku}, @Delta = {delta}, @Reason = {reason}, @ChangedBy = {changedBy}",
            cancellationToken);
    }
}
