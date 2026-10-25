namespace ApexPost.Domain.Entities;

public sealed record CounterTerminal
{
    public Guid Id { get; }
    public Guid BranchId { get; }
    public string Code { get; }
    public string Name { get; }
    public bool IsActive { get; }

    public CounterTerminal(Guid id, Guid branchId, string code, string name, bool isActive = true)
    {
        if (id == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Terminal and branch identifiers are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (code.Trim().Length is < 2 or > 12 || name.Trim().Length > 80)
            throw new ArgumentException("Terminal codes must be 2 to 12 characters and names cannot exceed 80 characters.");

        Id = id;
        BranchId = branchId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        IsActive = isActive;
    }

    public CounterTerminal WithActiveStatus(bool isActive) => new(Id, BranchId, Code, Name, isActive);
}
