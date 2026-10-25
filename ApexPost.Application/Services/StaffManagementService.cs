using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Services;

public sealed class StaffManagementService(
    IBranchRepository branchRepository,
    IStaffRepository staffRepository,
    IStaffPasswordService passwordService,
    ICounterTerminalRepository terminalRepository)
{
    public Task<IReadOnlyList<Branch>> GetBranchesAsync(CancellationToken cancellationToken = default) =>
        branchRepository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<StaffMember>> GetStaffAsync(CancellationToken cancellationToken = default) =>
        staffRepository.GetAllAsync(cancellationToken);

    public async Task CreateBranchAsync(
        string code,
        string name,
        string region,
        TimeOnly openingTime,
        TimeOnly closingTime,
        CancellationToken cancellationToken = default)
    {
        var branches = (await branchRepository.GetAllAsync(cancellationToken)).ToList();
        var branch = new Branch(Guid.NewGuid(), code, name, region, isOpen: true,
            openingTime: openingTime, closingTime: closingTime);

        if (branches.Any(existing => string.Equals(existing.Code, branch.Code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A branch with this code already exists.");
        }

        branches.Add(branch);
        await branchRepository.SaveAllAsync(branches, cancellationToken);
    }

    public async Task<IReadOnlyList<CounterTerminal>> GetTerminalsAsync(CancellationToken cancellationToken = default) =>
        await terminalRepository.GetAllAsync(cancellationToken);

    public async Task CreateTerminalAsync(Guid branchId, string code, string name, CancellationToken cancellationToken = default)
    {
        if (await branchRepository.GetByIdAsync(branchId, cancellationToken) is null)
            throw new InvalidOperationException("Select an existing branch for this terminal.");

        var terminals = (await terminalRepository.GetAllAsync(cancellationToken)).ToList();
        var terminal = new CounterTerminal(Guid.NewGuid(), branchId, code, name);
        if (terminals.Any(existing => existing.BranchId == branchId &&
            string.Equals(existing.Code, terminal.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A terminal with this code already exists at the selected branch.");

        terminals.Add(terminal);
        await terminalRepository.SaveAllAsync(terminals, cancellationToken);
    }

    public async Task SetTerminalActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var terminals = (await terminalRepository.GetAllAsync(cancellationToken)).ToList();
        var index = terminals.FindIndex(terminal => terminal.Id == id);
        if (index < 0)
            throw new KeyNotFoundException("The counter terminal could not be found.");
        terminals[index] = terminals[index].WithActiveStatus(isActive);
        await terminalRepository.SaveAllAsync(terminals, cancellationToken);
    }

    public async Task SetBranchOpenStatusAsync(Guid id, bool isOpen, CancellationToken cancellationToken = default)
    {
        var branches = (await branchRepository.GetAllAsync(cancellationToken)).ToList();
        var index = branches.FindIndex(branch => branch.Id == id);
        if (index < 0)
            throw new KeyNotFoundException("The branch could not be found.");
        branches[index] = branches[index].WithOpenStatus(isOpen);
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

    public async Task UpdateStaffAsync(
        Guid staffId,
        string email,
        string fullName,
        StaffRole role,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        var staff = (await staffRepository.GetAllAsync(cancellationToken)).ToList();
        var index = staff.FindIndex(member => member.Id == staffId);
        if (index < 0)
        {
            throw new KeyNotFoundException("The staff account could not be found.");
        }

        var target = staff[index];
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (staff.Any(existing => existing.Id != staffId &&
            string.Equals(existing.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
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

        if (target.IsActive && target.Role == StaffRole.Administrator && role != StaffRole.Administrator &&
            staff.Count(member => member.IsActive && member.Role == StaffRole.Administrator) <= 1)
        {
            throw new InvalidOperationException("The last active administrator cannot be changed to another role.");
        }

        staff[index] = target.WithProfile(normalizedEmail, fullName, role, branchId);
        await staffRepository.SaveAllAsync(staff, cancellationToken);
    }
}
