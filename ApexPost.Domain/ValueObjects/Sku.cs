namespace ApexPost.Domain.ValueObjects;

public readonly record struct Sku
{
    public string Value { get; }

    public Sku(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > 24 ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("SKU must contain 2 to 24 letters, numbers, or hyphens.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
