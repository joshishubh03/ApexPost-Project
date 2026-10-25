using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests;

public sealed class BusinessRuleTests
{
    [Fact]
    public void Low_stock_rule_uses_an_injected_predicate()
    {
        var rule = new LowStockRule(static (quantity, reorderLevel) => quantity < reorderLevel);

        Assert.True(rule.IsLowStock(4, 5));
        Assert.False(rule.IsLowStock(5, 5));
    }

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
    public void Shipment_receipt_requires_recipient_address_and_dimensions_together()
    {
        Assert.Throws<ArgumentException>(() => new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-12345678", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            [], Guid.NewGuid(), "Local", ShippingServiceLevel.Standard, 1m, 25m, PaymentMethod.Cash,
            shippingRecipientName: "Recipient", shippingAddress: "1 Main Street"));
    }

    [Fact]
    public void Letter_shipment_requires_destination_but_not_parcel_dimensions()
    {
        var transaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-LETTER01", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            [], Guid.NewGuid(), "Local", ShippingServiceLevel.Standard, 0.1m, 10m, PaymentMethod.Cash,
            shippingRecipientName: "Recipient", shippingAddress: "1 Main Street, Central",
            shipmentType: ShipmentType.Letter);

        Assert.Equal(ShipmentType.Letter, transaction.ShipmentType);
        Assert.Null(transaction.ParcelLengthCentimeters);
        Assert.Equal(10m, transaction.ShippingCharge);
    }

    [Fact]
    public void Letter_shipment_rejects_parcel_dimensions()
    {
        Assert.Throws<ArgumentException>(() => new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-LETTER02", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            [], Guid.NewGuid(), "Local", ShippingServiceLevel.Standard, 0.1m, 10m, PaymentMethod.Cash,
            shippingRecipientName: "Recipient", shippingAddress: "1 Main Street, Central",
            parcelLengthCentimeters: 10m, parcelWidthCentimeters: 5m, parcelHeightCentimeters: 1m,
            shipmentType: ShipmentType.Letter));
    }

    [Fact]
    public void Transaction_line_snapshots_tax_rate_and_rounded_tax_amount()
    {
        var line = new TransactionLine(Guid.NewGuid(), "SKU-1", "Taxed item", 2, 100m, "Retail", 5m);

        Assert.Equal("Retail", line.TaxCategory);
        Assert.Equal(5m, line.TaxRatePercent);
        Assert.Equal(10m, line.TaxAmount);
    }

    [Fact]
    public void Transaction_total_includes_tax_and_shipping()
    {
        var line = new TransactionLine(Guid.NewGuid(), "SKU-1", "Taxed item", 1, 100m, "Standard", 5m);
        var transaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-TAX00001", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            [line], Guid.NewGuid(), "Local", ShippingServiceLevel.Standard, 1m, 25m, PaymentMethod.Cash);

        Assert.Equal(100m, transaction.Subtotal);
        Assert.Equal(5m, transaction.TaxTotal);
        Assert.Equal(130m, transaction.TotalAmount);
    }

    [Fact]
    public void Transaction_total_includes_money_order_principal_and_fee()
    {
        var line = new TransactionLine(Guid.NewGuid(), "SKU-1", "Retail item", 1, 100m, "Standard", 5m);
        var transaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-MO00001", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            [line], null, null, null, null, 0m, PaymentMethod.Cash,
            moneyOrderId: Guid.NewGuid(), moneyOrderAmount: 250m, moneyOrderFee: 50m);

        Assert.Equal(100m, transaction.Subtotal);
        Assert.Equal(5m, transaction.TaxTotal);
        Assert.Equal(405m, transaction.TotalAmount);
    }

    [Fact]
    public void Transaction_can_contain_only_a_money_order()
    {
        var transaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-MO00002", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            [], null, null, null, null, 0m, PaymentMethod.Card,
            moneyOrderId: Guid.NewGuid(), moneyOrderAmount: 250m, moneyOrderFee: 50m);

        Assert.Equal(300m, transaction.TotalAmount);
        Assert.Empty(transaction.Lines);
    }

    [Fact]
    public async Task Counter_checkout_rejects_a_closed_branch_without_writing_a_receipt()
    {
        var fixture = new CounterCheckoutFixture(branchIsOpen: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CompleteSaleAsync());

        Assert.Empty(fixture.TransactionRepository.Transactions);
        Assert.Equal(10, fixture.InventoryRepository.Items.Single().QuantityOnHand);
    }

    [Fact]
    public async Task Counter_checkout_rejects_inactive_staff()
    {
        var fixture = new CounterCheckoutFixture(staffIsActive: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.CompleteSaleAsync());

        Assert.Empty(fixture.TransactionRepository.Transactions);
    }

    [Fact]
    public async Task Counter_checkout_rejects_staff_assigned_to_another_branch()
    {
        var fixture = new CounterCheckoutFixture(staffBranchId: Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.CompleteSaleAsync());

        Assert.Empty(fixture.TransactionRepository.Transactions);
    }

    [Fact]
    public async Task Counter_checkout_rejects_insufficient_stock_without_writing_a_receipt()
    {
        var fixture = new CounterCheckoutFixture(stockOnHand: 0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CompleteSaleAsync());

        Assert.Contains("not enough stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.TransactionRepository.Transactions);
        Assert.Equal(0, fixture.InventoryRepository.Items.Single().QuantityOnHand);
    }

    [Fact]
    public async Task Counter_checkout_links_money_order_and_retail_to_one_receipt()
    {
        var branch = new Branch(Guid.NewGuid(), "AP-020", "Mixed Basket Branch", "Central", true);
        var clerk = new StaffMember(Guid.NewGuid(), "clerk@test.local", "Checkout Clerk", StaffRole.CounterClerk,
            branch.Id, true, "hash");
        var terminal = new CounterTerminal(Guid.NewGuid(), branch.Id, "T-01", "Front counter");
        var product = new Product(Guid.NewGuid(), "SKU-1", "Retail item", "Description", "Retail", 100m, true, "Standard");
        var inventory = new FakeInventoryRepository([new InventoryItem(branch.Id, product.Id, 10, 2, [])]);
        var transactions = new FakeCounterRepository();
        var orders = new FakeMoneyOrderRepository();
        var verification = new FakeVerificationRepository();
        var branches = new FakeBranchRepository(branch);
        var staff = new FakeStaffRepository(clerk);
        var products = new FakeProductRepository([product]);
        var publisher = new FakeEventPublisher();
        var moneyOrderPolicy = new MoneyOrderPolicy(10_000m, 50m, "DEMO-VALID-");
        var moneyOrderService = new MoneyOrderService(branches, staff, orders, transactions, verification,
            new FakeIdentityVerificationService(true), publisher, moneyOrderPolicy);
        var inventoryService = new InventoryManagementService(inventory, products, branches, publisher);
        var shippingService = new ShippingRateService(new FakeShippingZoneRepository(), new FakeShippingRateRepository());
        var checkout = new CounterTransactionService(branches, products, inventory, transactions, staff, publisher,
            inventoryService, shippingService,
            new TaxPolicy(new Dictionary<string, decimal> { ["Standard"] = 5m, ["Shipping"] = 0m }),
            new FakeTerminalRepository([terminal]), moneyOrderService, moneyOrderPolicy);

        var receipt = await checkout.CompleteSaleAsync(
            branch.Id, clerk.Id, new Dictionary<Guid, int> { [product.Id] = 1 }, PaymentMethod.Cash,
            null, null, null, null, null, null, null, null, terminal.Id,
            10_000m, "Sender", "Recipient", null, null, "mixed-checkout-test");

        Assert.Single(transactions.Transactions);
        Assert.Single(orders.Orders);
        Assert.Equal(orders.Orders[0].Id, receipt.MoneyOrderId);
        Assert.Equal(10_000m, receipt.MoneyOrderAmount);
        Assert.Equal(50m, receipt.MoneyOrderFee);
        Assert.Equal(10_155m, receipt.TotalAmount);
        Assert.Equal(9, inventory.Items.Single().QuantityOnHand);
    }

    [Fact]
    public void Transaction_snapshots_shipping_tax_rate_and_amount()
    {
        var transaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-TAX00002", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            [], Guid.NewGuid(), "Local", ShippingServiceLevel.Standard, 1m, 25m, PaymentMethod.Cash,
            shippingTaxRatePercent: 5m);

        Assert.Equal(5m, transaction.ShippingTaxRatePercent);
        Assert.Equal(1.25m, transaction.ShippingTaxAmount);
        Assert.Equal(1.25m, transaction.TaxTotal);
        Assert.Equal(26.25m, transaction.TotalAmount);
    }

    [Fact]
    public void Tax_policy_looks_up_categories_without_case_sensitivity_and_rejects_unknown_categories()
    {
        var policy = new TaxPolicy(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Retail"] = 5m
        });

        Assert.Equal(5m, policy.GetRatePercent("retail"));
        Assert.Throws<InvalidOperationException>(() => policy.GetRatePercent("Unconfigured"));
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

    [Fact]
    public async Task Money_order_refund_records_manager_and_reason_once()
    {
        var fixture = new MoneyOrderFixture(verificationResult: true);
        var order = await fixture.Service.IssueAsync(
            fixture.Branch.Id, fixture.Staff.Id, "Sender", "Recipient", 100m,
            null, null, "correlation-issue");

        var refunded = await fixture.Service.RefundAsync(
            fixture.Branch.Id, order.Id, fixture.Staff.Id, "Customer cancellation", "correlation-refund");

        Assert.Equal(MoneyOrderStatus.Refunded, refunded.Status);
        Assert.Equal(fixture.Staff.Id, refunded.RefundedByStaffId);
        Assert.Equal("Customer cancellation", refunded.RefundReason);
        Assert.Equal(MoneyOrderStatus.Refunded, Assert.Single(fixture.MoneyOrderRepository.Orders).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RefundAsync(
            fixture.Branch.Id, order.Id, fixture.Staff.Id, "Second attempt", "correlation-refund"));
    }

    [Fact]
    public void Existing_money_order_json_defaults_to_issued_status()
    {
        var json = """{"id":"21000000-0000-0000-0000-000000000001","moneyOrderNumber":"APM-20261025-00000001","branchId":"22000000-0000-0000-0000-000000000001","staffId":"23000000-0000-0000-0000-000000000001","occurredAt":"2026-10-25T10:00:00+00:00","senderName":"Sender","recipientName":"Recipient","amount":100,"fee":50,"totalAmount":150,"verificationStatus":"NotRequired","verificationRecordId":null,"documentType":null,"maskedDocumentIdentifier":null}""";

        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var order = System.Text.Json.JsonSerializer.Deserialize<MoneyOrder>(json, options);

        Assert.NotNull(order);
        Assert.Equal(MoneyOrderStatus.Issued, order.Status);
    }

    [Fact]
    public async Task Counter_clerk_cannot_refund_a_money_order()
    {
        var fixture = new MoneyOrderFixture(verificationResult: true, role: StaffRole.CounterClerk);
        var order = await fixture.Service.IssueAsync(
            fixture.Branch.Id, fixture.Staff.Id, "Sender", "Recipient", 100m,
            null, null, "correlation-issue");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.RefundAsync(
            fixture.Branch.Id, order.Id, fixture.Staff.Id, "Customer cancellation", "correlation-refund"));

        Assert.Equal(MoneyOrderStatus.Issued, Assert.Single(fixture.MoneyOrderRepository.Orders).Status);
    }

    [Fact]
    public async Task Money_order_linked_to_counter_receipt_must_be_refunded_through_receipt()
    {
        var fixture = new MoneyOrderFixture(verificationResult: true);
        var order = await fixture.Service.IssueAsync(
            fixture.Branch.Id, fixture.Staff.Id, "Sender", "Recipient", 100m,
            null, null, "correlation-issue");
        fixture.TransactionRepository.Transactions.Add(new CounterTransaction(
            Guid.NewGuid(), "APT-20261025-LINK0001", fixture.Branch.Id, fixture.Staff.Id, DateTimeOffset.UtcNow,
            [], null, null, null, null, 0m, PaymentMethod.Cash,
            moneyOrderId: order.Id, moneyOrderAmount: order.Amount, moneyOrderFee: order.Fee));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RefundAsync(
            fixture.Branch.Id, order.Id, fixture.Staff.Id, "Cancel", "correlation-refund"));

        Assert.Equal(MoneyOrderStatus.Issued, fixture.MoneyOrderRepository.Orders.Single().Status);
    }

    private sealed class CounterCheckoutFixture
    {
        public Branch Branch { get; }
        public StaffMember Staff { get; }
        public Product Product { get; }
        public CounterTerminal Terminal { get; }
        public FakeInventoryRepository InventoryRepository { get; }
        public FakeCounterRepository TransactionRepository { get; }
        public CounterTransactionService Service { get; }

        public CounterCheckoutFixture(
            bool branchIsOpen = true,
            bool staffIsActive = true,
            Guid? staffBranchId = null,
            int stockOnHand = 10)
        {
            Branch = new Branch(Guid.NewGuid(), "AP-021", "Checkout Branch", "Central", branchIsOpen);
            Staff = new StaffMember(Guid.NewGuid(), "clerk@apexpost.local", "Checkout Clerk", StaffRole.CounterClerk,
                staffBranchId ?? Branch.Id, staffIsActive, "hash");
            Product = new Product(Guid.NewGuid(), "CHECKOUT-1", "Test item", "Test description", "Retail", 25m, true, "Standard");
            Terminal = new CounterTerminal(Guid.NewGuid(), Branch.Id, "T-01", "Front counter");
            InventoryRepository = new FakeInventoryRepository([new InventoryItem(Branch.Id, Product.Id, stockOnHand, 2, [])]);
            TransactionRepository = new FakeCounterRepository();

            var branches = new FakeBranchRepository(Branch);
            var staff = new FakeStaffRepository(Staff);
            var products = new FakeProductRepository([Product]);
            var publisher = new FakeEventPublisher();
            var moneyOrderPolicy = new MoneyOrderPolicy(10_000m, 50m, "DEMO-VALID-");
            var moneyOrderService = new MoneyOrderService(
                branches,
                staff,
                new FakeMoneyOrderRepository(),
                TransactionRepository,
                new FakeVerificationRepository(),
                new FakeIdentityVerificationService(true),
                publisher,
                moneyOrderPolicy);
            var inventoryService = new InventoryManagementService(InventoryRepository, products, branches, publisher);
            var shippingService = new ShippingRateService(new FakeShippingZoneRepository(), new FakeShippingRateRepository());

            Service = new CounterTransactionService(
                branches,
                products,
                InventoryRepository,
                TransactionRepository,
                staff,
                publisher,
                inventoryService,
                shippingService,
                new TaxPolicy(new Dictionary<string, decimal> { ["Standard"] = 5m, ["Shipping"] = 0m }),
                new FakeTerminalRepository([Terminal]),
                moneyOrderService,
                moneyOrderPolicy);
        }

        public Task<CounterTransaction> CompleteSaleAsync() => Service.CompleteSaleAsync(
            Branch.Id,
            Staff.Id,
            new Dictionary<Guid, int> { [Product.Id] = 1 },
            PaymentMethod.Cash,
            shippingRateId: null,
            shipmentType: null,
            shippingWeightKilograms: null,
            shippingRecipientName: null,
            shippingAddress: null,
            parcelLengthCentimeters: null,
            parcelWidthCentimeters: null,
            parcelHeightCentimeters: null,
            terminalId: Terminal.Id,
            moneyOrderAmount: null,
            moneyOrderSenderName: null,
            moneyOrderRecipientName: null,
            moneyOrderDocumentType: null,
            moneyOrderDocumentIdentifier: null,
            correlationId: "counter-checkout-test");
    }

    private sealed class MoneyOrderFixture
    {
        public Branch Branch { get; }
        public StaffMember Staff { get; }
        public FakeMoneyOrderRepository MoneyOrderRepository { get; }
        public FakeVerificationRepository VerificationRepository { get; }
        public FakeCounterRepository TransactionRepository { get; }
        public MoneyOrderService Service { get; }

        public MoneyOrderFixture(bool verificationResult, StaffRole role = StaffRole.BranchManager)
        {
            Branch = new Branch(Guid.NewGuid(), "AP-001", "Central", "North", true);
            Staff = new StaffMember(Guid.NewGuid(), "manager@apexpost.local", "Staff", role, Branch.Id, true, "hash");
            MoneyOrderRepository = new FakeMoneyOrderRepository();
            VerificationRepository = new FakeVerificationRepository();
            TransactionRepository = new FakeCounterRepository();
            Service = new MoneyOrderService(
                new FakeBranchRepository(Branch),
                new FakeStaffRepository(Staff),
                MoneyOrderRepository,
                TransactionRepository,
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

    private sealed class FakeProductRepository(IReadOnlyList<Product> products) : IProductRepository
    {
        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(products);
        public Task SaveAllAsync(IEnumerable<Product> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeInventoryRepository(IEnumerable<InventoryItem> items) : IInventoryRepository
    {
        public List<InventoryItem> Items { get; private set; } = items.ToList();
        public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(Items);
        public Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
        {
            Items = items.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCounterRepository : ICounterTransactionRepository
    {
        public List<CounterTransaction> Transactions { get; private set; } = [];
        public Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CounterTransaction>>(Transactions);
        public Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default)
        {
            Transactions = transactions.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTerminalRepository(IReadOnlyList<CounterTerminal> terminals) : ICounterTerminalRepository
    {
        public Task<IReadOnlyList<CounterTerminal>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(terminals);
        public Task SaveAllAsync(IEnumerable<CounterTerminal> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeShippingZoneRepository : IShippingZoneRepository
    {
        public Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShippingZone>>([]);
        public Task SaveAllAsync(IEnumerable<ShippingZone> zones, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeShippingRateRepository : IShippingRateRepository
    {
        public Task<IReadOnlyList<ShippingRate>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShippingRate>>([]);
        public Task SaveAllAsync(IEnumerable<ShippingRate> rates, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
