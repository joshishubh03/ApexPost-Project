using System.Runtime.CompilerServices;
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

        var sql = FormattableStringFactory.Create(
            "EXEC dbo.usp_DailyBranchSales @BranchCode = {0}, @Date = {1}",
            branchCode,
            saleDate);

        return await dbContext.Database
            .SqlQuery<DailyBranchSalesRow>(sql)
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

        var sql = FormattableStringFactory.Create(
            "EXEC dbo.usp_LowStockReport @BranchCode = {0}, @Threshold = {1}",
            string.IsNullOrWhiteSpace(branchCode) ? DBNull.Value : branchCode,
            threshold);

        return await dbContext.Database
            .SqlQuery<LowStockReportRow>(sql)
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

        var sql = FormattableStringFactory.Create(
            "EXEC dbo.usp_AdjustStock @BranchCode = {0}, @Sku = {1}, @Delta = {2}, @Reason = {3}, @ChangedBy = {4}",
            branchCode,
            sku,
            delta,
            reason,
            changedBy);

        await dbContext.Database.ExecuteSqlAsync(sql, cancellationToken);
    }
}
