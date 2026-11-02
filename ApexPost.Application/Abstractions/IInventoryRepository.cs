using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Stock repository (BranchStock + StockAdjustments history). Implementations stage changes only.
/// </summary>
public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<InventoryItem>> GetForBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).Where(item => item.BranchId == branchId).ToArray();

    /// <summary>Stages the given stock items (and any new movements) without touching other rows.</summary>
    async Task UpdateAsync(IReadOnlyCollection<InventoryItem> changedItems, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changedItems);
        if (changedItems.Count == 0)
            return;
        var all = (await GetAllAsync(cancellationToken)).ToList();
        foreach (var item in changedItems)
        {
            var index = all.FindIndex(candidate => candidate.BranchId == item.BranchId && candidate.ProductId == item.ProductId);
            if (index < 0)
                all.Add(item);
            else
                all[index] = item;
        }

        await SaveAllAsync(all, cancellationToken);
    }
}
