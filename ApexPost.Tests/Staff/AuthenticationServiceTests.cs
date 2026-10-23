using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests.Staff;

public sealed class AuthenticationServiceTests
{
    private static readonly Guid BranchId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Authenticate_returns_staff_and_branch_context_for_valid_credentials()
    {
        var staff = CreateStaff();
        var service = CreateService(staff, new Branch(BranchId, "AP-001", "Central", "North", true));

        var result = await service.AuthenticateAsync("CLERK@APEXPOST.LOCAL", "correct-password");

        Assert.NotNull(result);
        Assert.Equal(staff.Id, result.StaffId);
        Assert.Equal(StaffRole.CounterClerk, result.Role);
        Assert.Equal(BranchId, result.BranchId);
        Assert.Equal("AP-001", result.BranchCode);
    }

    [Fact]
    public async Task Authenticate_rejects_inactive_staff()
    {
        var staff = CreateStaff(isActive: false);
        var service = CreateService(staff, new Branch(BranchId, "AP-001", "Central", "North", true));

        var result = await service.AuthenticateAsync(staff.Email, "correct-password");

        Assert.Null(result);
    }

    [Fact]
    public async Task Authenticate_rejects_invalid_password()
    {
        var staff = CreateStaff();
        var service = CreateService(staff, new Branch(BranchId, "AP-001", "Central", "North", true));

        var result = await service.AuthenticateAsync(staff.Email, "wrong-password");

        Assert.Null(result);
    }

    private static StaffMember CreateStaff(bool isActive = true) => new(
        Guid.NewGuid(),
        "clerk@apexpost.local",
        "Counter Clerk",
        StaffRole.CounterClerk,
        BranchId,
        isActive,
        "stored-hash");

    private static AuthenticationService CreateService(StaffMember staff, Branch branch) =>
        new(new FakeStaffRepository(staff), new FakeBranchRepository(branch), new FakePasswordService());

    private sealed class FakeStaffRepository(StaffMember staff) : IStaffRepository
    {
        public Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StaffMember>>([staff]);

        public Task<StaffMember?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<StaffMember?>(
                string.Equals(email, staff.Email, StringComparison.OrdinalIgnoreCase) ? staff : null);

        public Task SaveAllAsync(IEnumerable<StaffMember> staffMembers, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeBranchRepository(Branch branch) : IBranchRepository
    {
        public Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Branch>>([branch]);

        public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Branch?>(id == branch.Id ? branch : null);

        public Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakePasswordService : IStaffPasswordService
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(StaffMember staff, string password) =>
            string.Equals(password, "correct-password", StringComparison.Ordinal);
    }
}
