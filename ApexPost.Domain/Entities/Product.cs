namespace ApexPost.Domain.Entities;

public sealed record Product
{
    public Guid Id { get; }
    public string Sku { get; }
    public string Name { get; }
    public string Description { get; }
    public string Category { get; }
    public decimal Price { get; }
    public bool IsActive { get; }

    public Product(Guid id, string sku, string name, string description, string category, decimal price, bool isActive)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A product ID is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (price < 0 || decimal.Round(price, 2) != price)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be non-negative and have no more than two decimal places.");
        }

        var normalizedSku = sku.Trim().ToUpperInvariant();
        if (normalizedSku.Length is < 2 or > 24 ||
            normalizedSku.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("SKU must contain 2 to 24 letters, numbers, or hyphens.", nameof(sku));
        }

        Id = id;
        Sku = normalizedSku;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = category.Trim();
        Price = price;
        IsActive = isActive;
    }

    public Product WithActiveStatus(bool isActive) => new(Id, Sku, Name, Description, Category, Price, isActive);
}
