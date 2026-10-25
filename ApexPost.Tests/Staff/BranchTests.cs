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
    public void Branch_stores_valid_local_operating_hours()
    {
        var branch = new Branch(Guid.NewGuid(), "ap-018", "West Counter", "West", true,
            new TimeOnly(8, 30), new TimeOnly(17, 45));

        Assert.Equal(new TimeOnly(8, 30), branch.OpeningTime);
        Assert.Equal(new TimeOnly(17, 45), branch.ClosingTime);
        Assert.Equal(branch.OpeningTime, branch.WithOpenStatus(false).OpeningTime);
    }

    [Fact]
    public void Branch_rejects_closing_time_before_opening_time()
    {
        Assert.Throws<ArgumentException>(() => new Branch(Guid.NewGuid(), "ap-019", "West Counter", "West", true,
            new TimeOnly(18, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void Legacy_branch_json_uses_demo_hours_when_hours_are_missing()
    {
        var branch = System.Text.Json.JsonSerializer.Deserialize<Branch>(
            """{"id":"10000000-0000-0000-0000-000000000001","code":"AP-001","name":"Central","region":"North","isOpen":true}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.NotNull(branch);
        Assert.Equal(new TimeOnly(9, 0), branch.OpeningTime);
        Assert.Equal(new TimeOnly(18, 0), branch.ClosingTime);
    }

    [Fact]
    public void Counter_terminal_normalizes_code_and_can_be_deactivated()
    {
        var terminal = new CounterTerminal(Guid.NewGuid(), Guid.NewGuid(), "till-1", "Front till");

        Assert.Equal("TILL-1", terminal.Code);
        Assert.False(terminal.WithActiveStatus(false).IsActive);
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
