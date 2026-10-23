using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Services;

public sealed class StaffManagementService(
    IBranchRepository branchRepository,
    IStaffRepository staffRepository,
    IStaffPasswordService passwordService)
{
    public Task<IReadOnlyList<Branch>> GetBranchesAsync(CancellationToken cancellationToken = default) =>
        branchRepository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<StaffMember>> GetStaffAsync(CancellationToken cancellationToken = default) =>
        staffRepository.GetAllAsync(cancellationToken);

    public async Task CreateBranchAsync(
        string code,
        string name,
        string region,
        CancellationToken cancellationToken = default)
    {
        var branches = (await branchRepository.GetAllAsync(cancellationToken)).ToList();
        var branch = new Branch(Guid.NewGuid(), code, name, region, isOpen: true);

        if (branches.Any(existing => string.Equals(existing.Code, branch.Code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A branch with this code already exists.");
        }

        branches.Add(branch);
        await branchRepository.SaveAllAsync(branches, cancellationToken);
    }

    public async Task CreateStaffAsync(
        string email,
        string fullName,
        StaffRole role,
        Guid? branchId,
        string initialPassword,
        CancellationToken cancellationToken = default)
    {
        var staff = (await staffRepository.GetAllAsync(cancellationToken)).ToList();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (staff.Any(existing => string.Equals(existing.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A staff account with this email already exists.");
        }

        if (role != StaffRole.Administrator)
        {
            if (branchId is null || await branchRepository.GetByIdAsync(branchId.Value, cancellationToken) is null)
            {
                throw new InvalidOperationException("Select an existing branch for this staff role.");
            }
        }
        else if (branchId is not null && await branchRepository.GetByIdAsync(branchId.Value, cancellationToken) is null)
        {
            throw new InvalidOperationException("The selected branch does not exist.");
        }

        var member = new StaffMember(
            Guid.NewGuid(), normalizedEmail, fullName, role, branchId, isActive: true,
            passwordService.Hash(initialPassword));
        staff.Add(member);
        await staffRepository.SaveAllAsync(staff, cancellationToken);
    }

    public async Task SetStaffActiveAsync(
        Guid staffId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var staff = (await staffRepository.GetAllAsync(cancellationToken)).ToList();
        var index = staff.FindIndex(member => member.Id == staffId);
        if (index < 0)
        {
            throw new KeyNotFoundException("The staff account could not be found.");
        }

        var target = staff[index];
        if (!isActive && target.Role == StaffRole.Administrator && target.IsActive &&
            staff.Count(member => member.Role == StaffRole.Administrator && member.IsActive) <= 1)
        {
            throw new InvalidOperationException("The last active administrator cannot be deactivated.");
        }

        staff[index] = target.WithActiveStatus(isActive);
        await staffRepository.SaveAllAsync(staff, cancellationToken);
    }
}
