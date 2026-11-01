using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed record Product
{
    public Guid Id { get; }
    public string Sku { get; }
    public string Name { get; }
    public string Description { get; }
    public string Category { get; }
    public string TaxCategory { get; }
    public decimal Price { get; }
    public bool IsActive { get; }

    public Product(
        Guid id,
        string sku,
        string name,
        string description,
        string category,
        decimal price,
        bool isActive,
        string taxCategory = "Standard")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A product ID is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxCategory);
        if (price < 0 || decimal.Round(price, 2) != price)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be non-negative and have no more than two decimal places.");
        }

        var normalizedSku = new Sku(sku).Value;

        Id = id;
        Sku = normalizedSku;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = category.Trim();
        if (taxCategory.Trim().Length > 60)
        {
            throw new ArgumentException("Tax category cannot exceed 60 characters.", nameof(taxCategory));
        }

        TaxCategory = taxCategory.Trim();
        Price = price;
        IsActive = isActive;
    }

    public Product WithActiveStatus(bool isActive) => new(Id, Sku, Name, Description, Category, Price, isActive, TaxCategory);
}
