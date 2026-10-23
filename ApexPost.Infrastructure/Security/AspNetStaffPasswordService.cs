using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ApexPost.Infrastructure.Security;

public sealed class AspNetStaffPasswordService : IStaffPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(new object(), password);
    }

    public bool Verify(StaffMember staff, string password)
    {
        ArgumentNullException.ThrowIfNull(staff);
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(new object(), staff.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
