using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// SQL Server stock-adjustment command: the quantity change runs through dbo.usp_AdjustStock (which also
/// writes the StockAdjustments history row), the reorder level through a set-based ExecuteUpdate, and the
/// audit/notification rows through SaveChanges - all inside ONE explicit transaction.
/// </summary>
public interface IStockAdjustmentCommand
{
    Task<StockAdjustmentResult> AdjustAsync(
        Guid branchId,
        Guid productId,
        int quantityChange,
        int reorderLevel,
        string reason,
        Guid staffId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
