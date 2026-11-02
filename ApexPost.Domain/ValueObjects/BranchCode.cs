namespace ApexPost.Domain.ValueObjects;

/// <summary>Normalized branch code (2-12 ASCII letters, digits or hyphens), stored as varchar(12).</summary>
public readonly record struct BranchCode
{
    public const int MaxLength = 12;

    public string Value { get; }

    public BranchCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > MaxLength ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Branch codes must contain 2 to 12 letters, numbers, or hyphens.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
