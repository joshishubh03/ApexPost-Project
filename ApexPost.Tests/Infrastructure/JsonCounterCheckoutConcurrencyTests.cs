using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Repositories;
using ApexPost.Infrastructure.Security;

namespace ApexPost.Tests.Infrastructure;

public sealed class JsonCounterCheckoutConcurrencyTests
{
    [Fact]
    public async Task Concurrent_counter_sales_keep_every_receipt_and_stock_change_with_json_repositories()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"apexpost-checkout-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var coordinator = new JsonMutationCoordinator(directory);
            await coordinator.InitializeAsync();

            var branches = new JsonBranchRepository(directory);
            var staff = new JsonStaffRepository(directory);
            var products = new JsonProductRepository(directory);
            var inventory = new JsonInventoryRepository(directory);
            var transactions = new JsonCounterTransactionRepository(directory);
            var terminals = new JsonCounterTerminalRepository(directory);
            var moneyOrders = new JsonMoneyOrderRepository(directory);
            var verifications = new JsonIdentityVerificationRepository(directory);
            var zones = new JsonShippingZoneRepository(directory);
            var rates = new JsonShippingRateRepository(directory);
            var branch = new Branch(Guid.NewGuid(), "AP-901", "Concurrency Branch", "Central", true);
            var clerk = new StaffMember(
                Guid.NewGuid(), "clerk@concurrency.test", "Concurrency Clerk", StaffRole.CounterClerk,
                branch.Id, true, "test-password-hash");
            var product = new Product(Guid.NewGuid(), "RACE-01", "Race test product", "", "Stationery", 2m, true);
            var terminal = new CounterTerminal(Guid.NewGuid(), branch.Id, "T-01", "Concurrency register");

            await coordinator.RunAsync(async () =>
            {
                await branches.SaveAllAsync([branch]);
                await staff.SaveAllAsync([clerk]);
                await products.SaveAllAsync([product]);
                await inventory.SaveAllAsync([new InventoryItem(branch.Id, product.Id, 25, 1, [])]);
                await terminals.SaveAllAsync([terminal]);
                await transactions.SaveAllAsync([]);
                await moneyOrders.SaveAllAsync([]);
                await verifications.SaveAllAsync([]);
                await zones.SaveAllAsync([]);
                await rates.SaveAllAsync([]);
            });

            var eventPublisher = new ApplicationEventPublisher([]);
            var moneyOrderPolicy = new MoneyOrderPolicy(10_000m, 50m, "DEMO-VALID-");
            var moneyOrderService = new MoneyOrderService(
                branches, staff, moneyOrders, transactions, verifications,
                new SimulatedIdentityVerificationService(moneyOrderPolicy.DemoVerificationSuccessPrefix),
                eventPublisher, moneyOrderPolicy, coordinator);
            var inventoryService = new InventoryManagementService(
                inventory, products, branches, eventPublisher, coordinator);
            var shippingRateService = new ShippingRateService(zones, rates, coordinator);
            var checkoutService = new CounterTransactionService(
                branches, products, inventory, transactions, staff, eventPublisher,
                inventoryService, shippingRateService,
                new TaxPolicy(new Dictionary<string, decimal> { ["Standard"] = 5m, ["Shipping"] = 0m }),
                terminals, moneyOrderService, moneyOrderPolicy, coordinator);

            const int saleCount = 20;
            var sales = await Task.WhenAll(Enumerable.Range(0, saleCount).Select(_ =>
                checkoutService.CompleteSaleAsync(
                    branch.Id,
                    clerk.Id,
                    new Dictionary<Guid, int> { [product.Id] = 1 },
                    PaymentMethod.Cash,
                    shippingRateId: null,
                    shipmentType: null,
                    shippingWeightKilograms: null,
                    shippingRecipientName: null,
                    shippingAddress: null,
                    parcelLengthCentimeters: null,
                    parcelWidthCentimeters: null,
                    parcelHeightCentimeters: null,
                    terminal.Id,
                    moneyOrderAmount: null,
                    moneyOrderSenderName: null,
                    moneyOrderRecipientName: null,
                    moneyOrderDocumentType: null,
                    moneyOrderDocumentIdentifier: null,
                    correlationId: "json-checkout-concurrency-test")));

            Assert.Equal(saleCount, sales.Length);
            Assert.Equal(saleCount, (await transactions.GetAllAsync()).Count);
            var savedStock = Assert.Single(await inventory.GetAllAsync());
            Assert.Equal(25 - saleCount, savedStock.QuantityOnHand);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
