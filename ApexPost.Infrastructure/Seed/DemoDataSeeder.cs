using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Infrastructure.Seed;

public sealed class DemoDataSeeder(
    IBranchRepository branchRepository,
    IStaffRepository staffRepository,
    IStaffPasswordService passwordService,
    string demoPassword)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var branches = await branchRepository.GetAllAsync(cancellationToken);
        if (branches.Count == 0)
        {
            branches = Enumerable.Range(1, 5)
                .Select(index => new Branch(
                    Guid.Parse($"10000000-0000-0000-0000-{index:000000000000}"),
                    $"AP-{index:000}",
                    $"ApexPost Branch {index}",
                    $"Region {index}",
                    isOpen: true))
                .ToArray();
            await branchRepository.SaveAllAsync(branches, cancellationToken);
        }

        var staff = await staffRepository.GetAllAsync(cancellationToken);
        if (staff.Count != 0)
        {
            return;
        }

        var seededStaff = new List<StaffMember>();
        var branchIds = branches.Select(branch => branch.Id).ToArray();

        for (var branchIndex = 0; branchIndex < branchIds.Length; branchIndex++)
        {
            var branchNumber = branchIndex + 1;
            seededStaff.Add(CreateStaff(
                $"manager{branchNumber}@apexpost.local",
                $"Branch Manager {branchNumber}",
                StaffRole.BranchManager,
                branchIds[branchIndex],
                passwordService,
                demoPassword));

            for (var clerkIndex = 1; clerkIndex <= 2; clerkIndex++)
            {
                seededStaff.Add(CreateStaff(
                    $"clerk{branchNumber}{clerkIndex}@apexpost.local",
                    $"Counter Clerk {branchNumber}-{clerkIndex}",
                    StaffRole.CounterClerk,
                    branchIds[branchIndex],
                    passwordService,
                    demoPassword));
            }
        }

        seededStaff.Add(CreateStaff(
            "clerk-extra1@apexpost.local",
            "Counter Clerk Extra 1",
            StaffRole.CounterClerk,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "clerk-extra2@apexpost.local",
            "Counter Clerk Extra 2",
            StaffRole.CounterClerk,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "clerk-extra3@apexpost.local",
            "Counter Clerk Extra 3",
            StaffRole.CounterClerk,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "auditor1@apexpost.local",
            "ApexPost Auditor",
            StaffRole.Auditor,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "admin@apexpost.local",
            "ApexPost Administrator",
            StaffRole.Administrator,
            null,
            passwordService,
            demoPassword));

        await staffRepository.SaveAllAsync(seededStaff, cancellationToken);
    }

    private static StaffMember CreateStaff(
        string email,
        string fullName,
        StaffRole role,
        Guid? branchId,
        IStaffPasswordService passwordService,
        string password)
    {
        var staffId = GuidFromEmail(email);
        return new StaffMember(
            staffId,
            email,
            fullName,
            role,
            branchId,
            isActive: true,
            passwordService.Hash(password));
    }

    private static Guid GuidFromEmail(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

