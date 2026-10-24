namespace ApexPost.Domain.Entities;

public sealed record TransactionLine
{
    public Guid ProductId { get; }
    public string Sku { get; }
    public string ProductName { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }
    public decimal LineTotal { get; }

    public TransactionLine(Guid productId, string sku, string productName, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A product is required for a transaction line.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Transaction quantity must be positive.");
        }

        if (unitPrice < 0 || decimal.Round(unitPrice, 2) != unitPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be non-negative with at most two decimal places.");
        }

        ProductId = productId;
        Sku = sku.Trim();
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
    }
}
