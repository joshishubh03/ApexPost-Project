using ApexPost.Domain.Enums;

namespace ApexPost.Domain.Entities;

public sealed record StaffMember
{
    public Guid Id { get; }
    public string Email { get; }
    public string FullName { get; }
    public StaffRole Role { get; }
    public Guid? BranchId { get; }
    public bool IsActive { get; }
    public string PasswordHash { get; }

    public StaffMember(
        Guid id,
        string email,
        string fullName,
        StaffRole role,
        Guid? branchId,
        bool isActive,
        string passwordHash)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A staff ID is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length > 254 ||
            !System.Net.Mail.MailAddress.TryCreate(normalizedEmail, out var address) ||
            !string.Equals(address.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A valid email address is required.", nameof(email));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "A supported staff role is required.");
        }

        if (role != StaffRole.Administrator && branchId is null)
        {
            throw new ArgumentException("Non-administrator staff must be assigned to a branch.", nameof(branchId));
        }

        Id = id;
        Email = normalizedEmail;
        FullName = fullName.Trim();
        Role = role;
        BranchId = branchId;
        IsActive = isActive;
        PasswordHash = passwordHash;
    }

    public StaffMember WithActiveStatus(bool isActive) =>
        new(Id, Email, FullName, Role, BranchId, isActive, PasswordHash);

    public StaffMember WithProfile(string email, string fullName, StaffRole role, Guid? branchId) =>
        new(Id, email, fullName, role, branchId, IsActive, PasswordHash);

    public StaffMember WithBranch(Guid branchId) =>
        new(Id, Email, FullName, Role, branchId, IsActive, PasswordHash);
}
