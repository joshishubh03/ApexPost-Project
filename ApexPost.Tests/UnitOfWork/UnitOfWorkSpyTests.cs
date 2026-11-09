using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Tests.UnitOfWork;

/// <summary>
/// Application-service tests with fake repositories and a spy unit of work (no database):
/// a use case commits exactly once when it succeeds and never when it fails.
/// </summary>
public sealed class UnitOfWorkSpyTests
{
    private static readonly Guid BranchId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid ClerkId = Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid ManagerId = Guid.Parse("72000000-0000-0000-0000-000000000002");
    private static readonly Guid ProductId = Guid.Parse("73000000-0000-0000-0000-000000000001");
    private static readonly Guid TerminalId = Guid.Parse("74000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Counter_sale_stages_stock_and_transaction_and_commits_once()
    {
        var fixture = new Fixture(stock: 5);

        var transaction = await fixture.Service.CompleteSaleAsync(
            BranchId, ClerkId, new Dictionary<Guid, int> { [ProductId] = 2 }, PaymentMethod.Card,
            null, null, null, null, null, null, null, null, TerminalId,
            null, null, null, null, null, "spy-sale");

        Assert.Equal(1, fixture.UnitOfWork.Transactions);
        Assert.Equal(1, fixture.UnitOfWork.Commits);
        Assert.Equal(3, Assert.Single(fixture.Inventory.Items).QuantityOnHand);
        Assert.Equal(transaction.Id, Assert.Single(fixture.Transactions.Items).Id);
    }

    [Fact]
    public async Task Counter_sale_that_breaks_a_stock_rule_never_commits()
    {
        var fixture = new Fixture(stock: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CompleteSaleAsync(
            BranchId, ClerkId, new Dictionary<Guid, int> { [ProductId] = 2 }, PaymentMethod.Cash,
            null, null, null, null, null, null, null, null, TerminalId,
            null, null, null, null, null, "spy-sale-fail"));

        Assert.Equal(0, fixture.UnitOfWork.Commits);
        Assert.Equal(1, Assert.Single(fixture.Inventory.Items).QuantityOnHand);
        Assert.Empty(fixture.Transactions.Items);
    }

    [Fact]
    public async Task Mixed_basket_with_failed_identity_check_never_commits_the_outer_use_case()
    {
        var fixture = new Fixture(stock: 5);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CompleteSaleAsync(
            BranchId, ClerkId, new Dictionary<Guid, int> { [ProductId] = 1 }, PaymentMethod.Cash,
            null, null, null, null, null, null, null, null, TerminalId,
            20_000m, "Sender", "Recipient", IdentityDocumentType.Passport, "P1234567", "spy-mixed"));

        // The nested money-order use case joined the sale; neither committed.
        Assert.Equal(1, fixture.UnitOfWork.Transactions);
        Assert.Equal(0, fixture.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Void_by_branch_manager_commits_once_and_restocks()
    {
        var fixture = new Fixture(stock: 5);
        var sale = await fixture.Service.CompleteSaleAsync(
            BranchId, ClerkId, new Dictionary<Guid, int> { [ProductId] = 2 }, PaymentMethod.Cash,
            null, null, null, null, null, null, null, null, TerminalId,
            null, null, null, null, null, "spy-sale");

        var voided = await fixture.Service.ReverseAsync(BranchId, sale.Id, ManagerId, refund: false, "Customer changed mind", "spy-void");

        Assert.Equal(TransactionStatus.Voided, voided.Status);
        Assert.Equal(2, fixture.UnitOfWork.Commits);
        Assert.Equal(5, Assert.Single(fixture.Inventory.Items).QuantityOnHand);
    }

    [Fact]
    public async Task Void_by_counter_clerk_is_rejected_without_commit()
    {
        var fixture = new Fixture(stock: 5);
        var sale = await fixture.Service.CompleteSaleAsync(
            BranchId, ClerkId, new Dictionary<Guid, int> { [ProductId] = 1 }, PaymentMethod.Cash,
            null, null, null, null, null, null, null, null, TerminalId,
            null, null, null, null, null, "spy-sale");
        var commitsAfterSale = fixture.UnitOfWork.Commits;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.ReverseAsync(BranchId, sale.Id, ClerkId, refund: false, "Not allowed", "spy-void"));

        Assert.Equal(commitsAfterSale, fixture.UnitOfWork.Commits);
    }

    [Fact]
    public void Shipment_weight_limit_is_enforced_by_the_domain()
    {
        Assert.Equal(30m, new Weight(30m).Kilograms);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(30.01m));
    }

    [Fact]
    public void Identity_documents_can_only_be_recorded_masked()
    {
        Assert.Equal("****5678", IdentityDocumentMask.Mask("P0005678"));
        Assert.Equal(16, IdentityDocumentMask.Mask(new string('9', 40)).Length);
        Assert.Throws<ArgumentException>(() => new IdentityVerificationRecord(
            Guid.NewGuid(), BranchId, ClerkId, DateTimeOffset.UtcNow, IdentityDocumentType.Passport,
            "P12345678", 20_000m, IdentityVerificationStatus.Verified, "unmasked"));
    }

    private sealed class Fixture
    {
        public Fixture(int stock)
        {
            var branch = new Branch(BranchId, "SPY-01", "Spy Branch", "Test Region", isOpen: true);
            var staff = new InMemoryStaffRepository(
                new StaffMember(ClerkId, "clerk@spy.test", "Spy Clerk", StaffRole.CounterClerk, BranchId, true, "hash"),
                new StaffMember(ManagerId, "manager@spy.test", "Spy Manager", StaffRole.BranchManager, BranchId, true, "hash"));
            var branches = new InMemoryBranchRepository(branch);
            var products = new InMemoryProductRepository(new Product(ProductId, "BOX-1", "Box", "Small box", "Packaging", 10m, true));
            Inventory = new InMemoryInventoryRepository(new InventoryItem(BranchId, ProductId, stock, 0, []));
            Transactions = new InMemoryTransactionRepository();
            var terminals = new InMemoryTerminalRepository(new CounterTerminal(TerminalId, BranchId, "T-01", "Counter 1"));
            var publisher = new NullEventPublisher();
            UnitOfWork = new SpyUnitOfWork();
            IUnitOfWork coordinator = UnitOfWork;
            var policy = new MoneyOrderPolicy(10_000m, 50m, "DEMO-VALID-");
            var taxPolicy = new TaxPolicy(new Dictionary<string, decimal>
            {
                ["Standard"] = 0m,
                [TaxPolicy.ShippingCategory] = 0m
            });
            var moneyOrders = new MoneyOrderService(branches, staff, new InMemoryMoneyOrderRepository(), Transactions,
                new InMemoryVerificationRepository(), new AlwaysFailingIdentityVerification(), publisher, policy, coordinator);
            var inventoryService = new InventoryManagementService(Inventory, products, branches, publisher, coordinator);
            var shippingService = new ShippingRateService(new InMemoryZoneRepository(), new InMemoryRateRepository(), coordinator);
            Service = new CounterTransactionService(branches, products, Inventory, Transactions, staff, publisher,
                inventoryService, shippingService, taxPolicy, terminals, moneyOrders, policy, UnitOfWork);
        }

        public InMemoryInventoryRepository Inventory { get; }
        public InMemoryTransactionRepository Transactions { get; }
        public SpyUnitOfWork UnitOfWork { get; }
        public CounterTransactionService Service { get; }
    }
}
