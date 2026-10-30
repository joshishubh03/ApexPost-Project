using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services;

public sealed class InventoryManagementService(
    IInventoryRepository inventoryRepository,
    IProductRepository productRepository,
    IBranchRepository branchRepository,
    IApplicationEventPublisher eventPublisher)
{
    public async Task<IReadOnlyList<ProductInventorySummary>> GetBranchInventoryAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        if (await branchRepository.GetByIdAsync(branchId, cancellationToken) is null)
        {
            throw new KeyNotFoundException("The branch could not be found.");
        }

        var products = await productRepository.GetAllAsync(cancellationToken);
        var inventory = await inventoryRepository.GetAllAsync(cancellationToken);
        var branchInventory = inventory.Where(item => item.BranchId == branchId)
            .ToDictionary(item => item.ProductId);

        return products.Where(product => product.IsActive)
            .OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .Select(product =>
            {
                var hasRecord = branchInventory.TryGetValue(product.Id, out var item);
                return new ProductInventorySummary(
                    product.Id,
                    product.Sku,
                    product.Name,
                    product.Category,
                    product.TaxCategory,
                    product.Price,
                    item?.QuantityOnHand ?? 0,
                    item?.ReorderLevel ?? 0,
                    hasRecord,
                    hasRecord && item!.IsBelowReorderLevel,
                    item?.Movements.OrderByDescending(movement => movement.OccurredAt).Take(3).ToArray() ?? []);
            })
            .ToArray();
    }

    public Task ChangeStockAsync(
        Guid branchId,
        Guid productId,
        int quantityChange,
        int reorderLevel,
        string reason,
        Guid staffId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        InProcessMutationGate.RunAsync(
            InProcessMutationGate.CounterAndInventory,
            () => ChangeStockCoreAsync(
                branchId, productId, quantityChange, reorderLevel, reason, staffId, correlationId, cancellationToken),
            cancellationToken);

    private async Task ChangeStockCoreAsync(
        Guid branchId,
        Guid productId,
        int quantityChange,
        int reorderLevel,
        string reason,
        Guid staffId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        if (!branch.IsOpen)
        {
            throw new InvalidOperationException("Inventory cannot be changed for a closed branch.");
        }

        var product = (await productRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == productId);
        if (product is null || !product.IsActive)
        {
            throw new InvalidOperationException("Select an active catalogue product.");
        }

        var allItems = (await inventoryRepository.GetAllAsync(cancellationToken)).ToList();
        var index = allItems.FindIndex(item => item.BranchId == branchId && item.ProductId == productId);
        var item = index < 0
            ? InventoryItem.Empty(branchId, productId, reorderLevel)
            : allItems[index];

        var wasBelowReorderLevel = index >= 0 && item.IsBelowReorderLevel;
        var occurredAt = DateTimeOffset.UtcNow;
        var changed = item.ApplyChange(
            quantityChange,
            reorderLevel,
            reason,
            staffId,
            occurredAt);

        if (index < 0)
        {
            allItems.Add(changed);
        }
        else
        {
            allItems[index] = changed;
        }

        await inventoryRepository.SaveAllAsync(allItems, cancellationToken);

        if (changed.IsBelowReorderLevel && !wasBelowReorderLevel)
        {
            await eventPublisher.PublishAsync(new StockLowEvent(
                branchId, product.Id, product.Name, changed.QuantityOnHand, changed.ReorderLevel,
                staffId, occurredAt, correlationId), CancellationToken.None);
        }
    }
}
