namespace ApexPost.Domain.Entities;

public sealed record InventoryMovement
{
    public Guid StaffId { get; }
    public int QuantityChange { get; }
    public string Reason { get; }
    public DateTimeOffset OccurredAt { get; }

    public InventoryMovement(Guid staffId, int quantityChange, string reason, DateTimeOffset occurredAt)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("The staff member making the change is required.", nameof(staffId));
        }

        if (quantityChange == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityChange), "A stock change cannot be zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        StaffId = staffId;
        QuantityChange = quantityChange;
        Reason = reason.Trim();
        OccurredAt = occurredAt;
    }
}

public sealed record InventoryItem
{
    public Guid BranchId { get; }
    public Guid ProductId { get; }
    public int QuantityOnHand { get; }
    public int ReorderLevel { get; }
    public IReadOnlyList<InventoryMovement> Movements { get; }

    public InventoryItem(
        Guid branchId,
        Guid productId,
        int quantityOnHand,
        int reorderLevel,
        IReadOnlyList<InventoryMovement> movements)
    {
        if (branchId == Guid.Empty || productId == Guid.Empty)
        {
            throw new ArgumentException("A branch and product are required for an inventory item.");
        }

        if (quantityOnHand < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityOnHand), "Stock cannot be negative.");
        }

        if (reorderLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reorderLevel), "The reorder level cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(movements);
        BranchId = branchId;
        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        ReorderLevel = reorderLevel;
        Movements = movements.ToArray();
    }

    public static InventoryItem Empty(Guid branchId, Guid productId, int reorderLevel) =>
        new(branchId, productId, 0, reorderLevel, []);

    public InventoryItem ApplyChange(
        int quantityChange,
        int reorderLevel,
        string reason,
        Guid staffId,
        DateTimeOffset occurredAt)
    {
        var nextQuantity = checked(QuantityOnHand + quantityChange);
        if (nextQuantity < 0)
        {
            throw new InvalidOperationException("The stock change would make quantity on hand negative.");
        }

        var movement = new InventoryMovement(staffId, quantityChange, reason, occurredAt);
        return new InventoryItem(
            BranchId,
            ProductId,
            nextQuantity,
            reorderLevel,
            Movements.Append(movement).ToArray());
    }

    public bool IsBelowReorderLevel => QuantityOnHand <= ReorderLevel;
}
