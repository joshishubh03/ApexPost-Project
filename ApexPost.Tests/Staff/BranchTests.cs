using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests.Staff;

public sealed class BranchTests
{
    [Fact]
    public void Constructor_normalizes_branch_code()
    {
        var branch = new Branch(
            Guid.NewGuid(),
            "ap-017",
            "Central Counter",
            "North",
            isOpen: true);

        Assert.Equal("AP-017", branch.Code);
    }

    [Fact]
    public void Constructor_rejects_invalid_branch_code()
    {
        Assert.Throws<ArgumentException>(() => new Branch(
            Guid.NewGuid(),
            "branch with spaces",
            "Central Counter",
            "North",
            isOpen: true));
    }

    [Fact]
    public void Non_administrator_must_have_branch_assignment()
    {
        Assert.Throws<ArgumentException>(() => new StaffMember(
            Guid.NewGuid(),
            "clerk@apexpost.local",
            "Counter Clerk",
            StaffRole.CounterClerk,
            null,
            isActive: true,
            "test-hash"));
    }
}
