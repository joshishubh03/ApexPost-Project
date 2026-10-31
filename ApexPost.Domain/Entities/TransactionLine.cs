using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed record TransactionLine
{
    public Guid ProductId { get; }
    public string Sku { get; }
    public string ProductName { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }
    public decimal LineTotal { get; }
    public string TaxCategory { get; }
    public decimal TaxRatePercent { get; }
    public decimal TaxAmount { get; }

    public TransactionLine(
        Guid productId,
        string sku,
        string productName,
        int quantity,
        decimal unitPrice,
        string taxCategory = "Standard",
        decimal taxRatePercent = 0m)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A product is required for a transaction line.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxCategory);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Transaction quantity must be positive.");
        }

        if (unitPrice < 0 || decimal.Round(unitPrice, 2) != unitPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be non-negative with at most two decimal places.");
        }

        if (taxCategory.Trim().Length > 60 || taxRatePercent < 0 || taxRatePercent > 100 ||
            decimal.Round(taxRatePercent, 2) != taxRatePercent)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRatePercent), "Tax rate must be from 0 to 100 percent with no more than two decimal places.");
        }

        ProductId = productId;
        Sku = sku.Trim();
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        var unitPriceMoney = new Money(unitPrice);
        LineTotal = unitPriceMoney.Multiply(quantity).Amount;
        TaxCategory = taxCategory.Trim();
        TaxRatePercent = taxRatePercent;
        TaxAmount = new Money(LineTotal).Percentage(taxRatePercent).Amount;
    }
}
