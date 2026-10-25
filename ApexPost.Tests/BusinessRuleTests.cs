using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests;

public sealed class BusinessRuleTests
{
    [Fact]
    public void Inventory_change_cannot_make_stock_negative()
    {
        var inventory = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), 2, 1, []);

        Assert.Throws<InvalidOperationException>(() => inventory.ApplyChange(-3, 1, "Sale", Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Shipping_rate_rejects_weight_above_configured_limit()
    {
        var rate = new ShippingRate(Guid.NewGuid(), Guid.NewGuid(), ShippingServiceLevel.Standard, 20m, 5m, 2m, true);

        Assert.Throws<InvalidOperationException>(() => rate.CalculatePrice(2.01m));
    }

    [Fact]
    public void Shipping_rate_calculates_base_plus_weighted_charge()
    {
        var rate = new ShippingRate(Guid.NewGuid(), Guid.NewGuid(), ShippingServiceLevel.Express, 20m, 5m, 2m, true);

        Assert.Equal(27.50m, rate.CalculatePrice(1.5m));
    }

    [Fact]
    public async Task Money_order_at_threshold_does_not_require_identity_check()
    {
        var fixture = new MoneyOrderFixture(verificationResult: true);

        var order = await fixture.Service.IssueAsync(
            fixture.Branch.Id, fixture.Staff.Id, "Sender", "Recipient", 10_000m,
            null, null, "correlation-test");

        Assert.Equal(IdentityVerificationStatus.NotRequired, order.VerificationStatus);
        Assert.Empty(fixture.VerificationRepository.Records);
        Assert.Single(fixture.MoneyOrderRepository.Orders);
    }

    [Fact]
    public async Task Money_order_above_threshold_records_masked_successful_verification()
    {
        var fixture = new MoneyOrderFixture(verificationResult: true);

        var order = await fixture.Service.IssueAsync(
            fixture.Branch.Id, fixture.Staff.Id, "Sender", "Recipient", 10_001m,
            IdentityDocumentType.Passport, "ABCDE1234", "correlation-test");

        Assert.Equal(IdentityVerificationStatus.Verified, order.VerificationStatus);
        Assert.Equal("*****1234", order.MaskedDocumentIdentifier);
        Assert.Single(fixture.VerificationRepository.Records);
        Assert.DoesNotContain("ABCDE", fixture.VerificationRepository.Records[0].MaskedDocumentIdentifier);
    }

    [Fact]
    public async Task Failed_identity_verification_does_not_issue_money_order()
    {
        var fixture = new MoneyOrderFixture(verificationResult: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.IssueAsync(
            fixture.Branch.Id, fixture.Staff.Id, "Sender", "Recipient", 10_001m,
            IdentityDocumentType.NationalId, "IDENTIFIER9999", "correlation-test"));

        Assert.Empty(fixture.MoneyOrderRepository.Orders);
        Assert.Single(fixture.VerificationRepository.Records);
        Assert.Equal(IdentityVerificationStatus.Failed, fixture.VerificationRepository.Records[0].Status);
    }

    private sealed class MoneyOrderFixture
    {
        public Branch Branch { get; }
        public StaffMember Staff { get; }
        public FakeMoneyOrderRepository MoneyOrderRepository { get; }
        public FakeVerificationRepository VerificationRepository { get; }
        public MoneyOrderService Service { get; }

        public MoneyOrderFixture(bool verificationResult)
        {
            Branch = new Branch(Guid.NewGuid(), "AP-001", "Central", "North", true);
            Staff = new StaffMember(Guid.NewGuid(), "manager@apexpost.local", "Manager", StaffRole.BranchManager, Branch.Id, true, "hash");
            MoneyOrderRepository = new FakeMoneyOrderRepository();
            VerificationRepository = new FakeVerificationRepository();
            Service = new MoneyOrderService(
                new FakeBranchRepository(Branch),
                new FakeStaffRepository(Staff),
                MoneyOrderRepository,
                VerificationRepository,
                new FakeIdentityVerificationService(verificationResult),
                new FakeEventPublisher(),
                new MoneyOrderPolicy(10_000m, 50m, "DEMO-VALID-"));
        }
    }

    private sealed class FakeBranchRepository(Branch branch) : IBranchRepository
    {
        public Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Branch>>([branch]);
        public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(id == branch.Id ? branch : null);
        public Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStaffRepository(StaffMember staff) : IStaffRepository
    {
        public Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StaffMember>>([staff]);
        public Task<StaffMember?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult<StaffMember?>(staff);
        public Task SaveAllAsync(IEnumerable<StaffMember> staffMembers, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMoneyOrderRepository : IMoneyOrderRepository
    {
        public List<MoneyOrder> Orders { get; private set; } = [];
        public Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MoneyOrder>>(Orders);
        public Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default)
        {
            Orders = moneyOrders.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVerificationRepository : IIdentityVerificationRepository
    {
        public List<IdentityVerificationRecord> Records { get; private set; } = [];
        public Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IdentityVerificationRecord>>(Records);
        public Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default)
        {
            Records = records.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdentityVerificationService(bool result) : IIdentityVerificationService
    {
        public Task<bool> VerifyAsync(IdentityDocumentType documentType, string documentIdentifier, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FakeEventPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
