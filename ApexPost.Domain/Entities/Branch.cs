namespace ApexPost.Domain.Entities;

public sealed record Branch
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string Region { get; }
    public bool IsOpen { get; }
    public TimeOnly OpeningTime { get; }
    public TimeOnly ClosingTime { get; }

    public Branch(Guid id, string code, string name, string region, bool isOpen,
        TimeOnly openingTime = default, TimeOnly closingTime = default)
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
        if (openingTime == default && closingTime == default)
        {
            openingTime = new TimeOnly(9, 0);
            closingTime = new TimeOnly(18, 0);
        }
        OpeningTime = openingTime;
        ClosingTime = closingTime;
        if (OpeningTime >= ClosingTime)
        {
            throw new ArgumentException("Closing time must be later than opening time.", nameof(closingTime));
        }
    }

    public Branch WithOpenStatus(bool isOpen) => new(Id, Code, Name, Region, isOpen, OpeningTime, ClosingTime);
}
