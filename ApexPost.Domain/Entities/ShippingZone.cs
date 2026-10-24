namespace ApexPost.Domain.Entities;

public sealed record ShippingZone
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string Destination { get; }
    public bool IsActive { get; }

    public ShippingZone(Guid id, string code, string name, string destination, bool isActive)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A shipping zone ID is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length is < 2 or > 12 ||
            normalizedCode.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Zone codes must contain 2 to 12 letters, numbers, or hyphens.", nameof(code));
        }

        Id = id;
        Code = normalizedCode;
        Name = name.Trim();
        Destination = destination.Trim();
        IsActive = isActive;
    }
}
