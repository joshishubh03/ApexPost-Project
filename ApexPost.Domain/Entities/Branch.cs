namespace ApexPost.Domain.Entities;

public sealed record Branch
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string Region { get; }
    public bool IsOpen { get; }

    public Branch(Guid id, string code, string name, string region, bool isOpen)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A branch ID is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length is < 2 or > 12 ||
            normalizedCode.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Branch codes must contain 2 to 12 letters, numbers, or hyphens.", nameof(code));
        }

        Id = id;
        Code = normalizedCode;
        Name = name.Trim();
        Region = region.Trim();
        IsOpen = isOpen;
    }

    public Branch WithOpenStatus(bool isOpen) => new(Id, Code, Name, Region, isOpen);
}
