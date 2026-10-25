namespace ApexPost.Application.Models;

public sealed class LowStockRule(Func<int, int, bool> predicate)
{
    public bool IsLowStock(int quantityOnHand, int reorderLevel) =>
        predicate(quantityOnHand, reorderLevel);

    public static LowStockRule AtOrBelowReorderLevel { get; } =
        new(static (quantity, reorderLevel) => quantity <= reorderLevel);
}
