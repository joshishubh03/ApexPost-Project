using ApexPost.Domain.Entities;

namespace ApexPost.Application.Models;

public sealed record ProductInventorySummary(
    Guid ProductId,
    string Sku,
    string Name,
    string Category,
    decimal Price,
    int QuantityOnHand,
    int ReorderLevel,
    bool HasInventoryRecord,
    bool IsBelowReorderLevel,
    IReadOnlyList<InventoryMovement> RecentMovements);
