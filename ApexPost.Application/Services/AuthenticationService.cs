using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services;

public sealed class AuthenticationService(
    IStaffRepository staffRepository,
    IBranchRepository branchRepository,
    IStaffPasswordService passwordService) : IAuthenticationService
{
    public async Task<StaffSession?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var staff = await staffRepository.GetByEmailAsync(email, cancellationToken);
        if (staff is null || !passwordService.Verify(staff, password) || !staff.IsActive)
        {
            return null;
        }

        Branch? branch = null;
        if (staff.BranchId is { } branchId)
        {
            branch = await branchRepository.GetByIdAsync(branchId, cancellationToken);
            if (branch is null)
            {
                return null;
            }
        }

        return new StaffSession(
            staff.Id,
            staff.Email,
            staff.FullName,
            staff.Role,
            staff.BranchId,
            branch?.Code,
            branch?.Name);
    }
}
