using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Calls the stored procedures with EF Core's parameterized raw-SQL APIs. Every {value} in the interpolated
/// strings below becomes a SqlParameter (EF Core's FormattableString overloads) - there is no string
/// concatenation and no way for input to change the SQL text.
/// </summary>
public sealed class SqlServerStoredProcedureService(ApexPostDbContext context) : IStoredProcedureService
{
    /// <summary>THROW number used by every procedure when @BranchCode does not exist (see sql/stored-procedures.sql).</summary>
    public const int BranchNotFoundError = 50004;

    public async Task<IReadOnlyList<DailyBranchSalesRow>> GetDailyBranchSalesAsync(
        string branchCode,
        DateOnly saleDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        try
        {
            return await context.Database
                .SqlQuery<DailyBranchSalesRow>($"EXEC dbo.usp_DailyBranchSales @BranchCode = {branchCode}, @Date = {saleDate}")
                .ToListAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == BranchNotFoundError)
        {
            throw new KeyNotFoundException(exception.Message, exception);
        }
    }

    public async Task<IReadOnlyList<LowStockReportRow>> GetLowStockReportAsync(
        string? branchCode,
        int threshold,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        // NULL means "all branches"; DBNull is sent as a typed parameter value, never spliced into the SQL.
        object code = string.IsNullOrWhiteSpace(branchCode) ? DBNull.Value : branchCode.Trim();
        try
        {
            return await context.Database
                .SqlQuery<LowStockReportRow>($"EXEC dbo.usp_LowStockReport @BranchCode = {code}, @Threshold = {threshold}")
                .ToListAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == BranchNotFoundError)
        {
            throw new KeyNotFoundException(exception.Message, exception);
        }
    }

    public async Task<StockAdjustmentResult> AdjustStockAsync(
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
            throw new ArgumentOutOfRangeException(nameof(delta), "Stock delta cannot be zero.");

        var rows = await context.Database
            .SqlQuery<StockAdjustmentResult>(
                $"EXEC dbo.usp_AdjustStock @BranchCode = {branchCode}, @Sku = {sku}, @Delta = {delta}, @Reason = {reason}, @ChangedBy = {changedBy}")
            .ToListAsync(cancellationToken);
        return rows.Single();
    }
}
