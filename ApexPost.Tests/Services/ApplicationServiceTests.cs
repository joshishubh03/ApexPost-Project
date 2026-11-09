using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Tests.UnitOfWork;

namespace ApexPost.Tests.Services;

/// <summary>
/// Application services with in-memory repositories and a spy unit of work: business rules are enforced
/// before anything is staged, and every successful use case commits exactly once (failed ones never commit).
/// </summary>
public sealed class ApplicationServiceTests
{
    private static readonly Guid BranchId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly Guid StaffId = Guid.Parse("40000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task Creating_a_shipping_zone_commits_once()
    {
        var zones = new MutableZoneRepository();
        var unitOfWork = new SpyUnitOfWork();
        var service = new ShippingRateService(zones, new MutableRateRepository(), unitOfWork);

        await service.CreateZoneAsync("z-01", "Metro", "City centre");

        var zone = Assert.Single(zones.Items);
        Assert.Equal("Z-01", zone.Code);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task Duplicate_zone_code_is_rejected_case_insensitively_without_committing()
    {
        var zones = new MutableZoneRepository(new ShippingZone(Guid.NewGuid(), "Z-01", "Metro", "City", true));
        var unitOfWork = new SpyUnitOfWork();
        var service = new ShippingRateService(zones, new MutableRateRepository(), unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateZoneAsync("z-01", "Copy", "City"));

        Assert.Single(zones.Items);
        Assert.Equal(0, unitOfWork.Commits);
    }

    [Fact]
    public async Task Rate_for_an_inactive_zone_is_rejected()
    {
        var zone = new ShippingZone(Guid.NewGuid(), "Z-02", "Closed", "Nowhere", isActive: false);
        var rates = new MutableRateRepository();
        var service = new ShippingRateService(new MutableZoneRepository(zone), rates, new SpyUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRateAsync(zone.Id, ShippingServiceLevel.Standard, 50m, 10m, 20m));

        Assert.Empty(rates.Items);
    }

    [Fact]
    public async Task Second_rate_for_the_same_zone_and_service_level_is_rejected()
    {
        var zone = new ShippingZone(Guid.NewGuid(), "Z-03", "Regional", "Region", true);
        var rates = new MutableRateRepository(
            new ShippingRate(Guid.NewGuid(), zone.Id, ShippingServiceLevel.Express, 80m, 15m, 20m, true));
        var service = new ShippingRateService(new MutableZoneRepository(zone), rates, new SpyUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRateAsync(zone.Id, ShippingServiceLevel.Express, 90m, 15m, 20m));
        await service.CreateRateAsync(zone.Id, ShippingServiceLevel.Standard, 40m, 8m, 20m);

        Assert.Equal(2, rates.Items.Count);
    }

    [Fact]
    public async Task Quote_uses_the_rate_price_and_rejects_unknown_or_overweight_shipments()
    {
        var zone = new ShippingZone(Guid.NewGuid(), "Z-04", "Coastal", "Coast", true);
        var rate = new ShippingRate(Guid.NewGuid(), zone.Id, ShippingServiceLevel.Standard, 50m, 10m, 20m, true);
        var service = new ShippingRateService(new MutableZoneRepository(zone), new MutableRateRepository(rate), new SpyUnitOfWork());

        var quote = await service.QuoteAsync(rate.Id, 2.5m);

        Assert.Equal(rate.CalculatePrice(2.5m), quote.Price);
        Assert.Equal("Coastal", quote.ZoneName);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.QuoteAsync(Guid.NewGuid(), 1m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QuoteAsync(rate.Id, 25m));
    }

    [Fact]
    public async Task Schedule_hides_inactive_zones_and_rates_unless_asked()
    {
        var active = new ShippingZone(Guid.NewGuid(), "Z-05", "Active", "A", true);
        var inactive = new ShippingZone(Guid.NewGuid(), "Z-06", "Inactive", "B", false);
        var rates = new MutableRateRepository(
            new ShippingRate(Guid.NewGuid(), active.Id, ShippingServiceLevel.Standard, 10m, 1m, 10m, true),
            new ShippingRate(Guid.NewGuid(), active.Id, ShippingServiceLevel.Express, 20m, 2m, 10m, false));
        var service = new ShippingRateService(new MutableZoneRepository(active, inactive), rates, new SpyUnitOfWork());

        var visible = await service.GetScheduleAsync(includeInactive: false);
        var all = await service.GetScheduleAsync(includeInactive: true);

        var schedule = Assert.Single(visible);
        Assert.Single(schedule.Rates);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Stock_change_for_a_closed_branch_is_rejected_without_committing()
    {
        var fixture = new InventoryFixture(branchOpen: false, stock: 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ChangeAsync(-1));

        Assert.Equal(0, fixture.UnitOfWork.Commits);
        Assert.Equal(10, fixture.Inventory.Items.Single().QuantityOnHand);
    }

    [Fact]
    public async Task Stock_change_for_an_inactive_product_is_rejected()
    {
        var fixture = new InventoryFixture(branchOpen: true, stock: 10, productActive: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ChangeAsync(5));
    }

    [Fact]
    public async Task Stock_change_cannot_make_quantity_negative()
    {
        var fixture = new InventoryFixture(branchOpen: true, stock: 3);

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.ChangeAsync(-4));

        Assert.Equal(3, fixture.Inventory.Items.Single().QuantityOnHand);
        Assert.Equal(0, fixture.UnitOfWork.Commits);
    }

    [Fact]
    public async Task First_receipt_of_a_product_creates_the_stock_row_and_commits_once()
    {
        var fixture = new InventoryFixture(branchOpen: true, stock: null);

        await fixture.ChangeAsync(12, reorderLevel: 4);

        var item = Assert.Single(fixture.Inventory.Items);
        Assert.Equal(12, item.QuantityOnHand);
        Assert.Equal(4, item.ReorderLevel);
        Assert.Equal(1, fixture.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Crossing_the_reorder_level_publishes_one_low_stock_event()
    {
        var fixture = new InventoryFixture(branchOpen: true, stock: 10);

        await fixture.ChangeAsync(-8, reorderLevel: 5);
        await fixture.ChangeAsync(-1, reorderLevel: 5);

        var lowStock = Assert.Single(fixture.Publisher.Events.OfType<StockLowEvent>());
        Assert.Equal(2, lowStock.QuantityOnHand);
    }

    private sealed class InventoryFixture
    {
        public InventoryFixture(bool branchOpen, int? stock, bool productActive = true)
        {
            var branches = new InMemoryBranchRepository(new Branch(BranchId, "INV-01", "Inventory Branch", "Region", branchOpen));
            var products = new InMemoryProductRepository(
                new Product(ProductId, "BOX-9", "Box", "Box", "Packaging", 10m, productActive));
            Inventory = stock is { } quantity
                ? new InMemoryInventoryRepository(new InventoryItem(BranchId, ProductId, quantity, 0, []))
                : new InMemoryInventoryRepository();
            UnitOfWork = new SpyUnitOfWork();
            Publisher = new CapturingPublisher();
            Service = new InventoryManagementService(Inventory, products, branches, Publisher, UnitOfWork);
        }

        public InMemoryInventoryRepository Inventory { get; }
        public SpyUnitOfWork UnitOfWork { get; }
        public CapturingPublisher Publisher { get; }
        public InventoryManagementService Service { get; }

        public Task ChangeAsync(int change, int reorderLevel = 0) =>
            Service.ChangeStockAsync(BranchId, ProductId, change, reorderLevel, "Unit test", StaffId, "unit-test");
    }

    private sealed class CapturingPublisher : IApplicationEventPublisher
    {
        public List<object> Events { get; } = [];

        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(applicationEvent!);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableZoneRepository(params ShippingZone[] zones) : IShippingZoneRepository
    {
        public List<ShippingZone> Items { get; } = [.. zones];
        public Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShippingZone>>(Items.ToArray());
        public Task SaveAllAsync(IEnumerable<ShippingZone> zones, CancellationToken cancellationToken = default)
        {
            var copy = zones.ToArray();
            Items.Clear();
            Items.AddRange(copy);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableRateRepository(params ShippingRate[] rates) : IShippingRateRepository
    {
        public List<ShippingRate> Items { get; } = [.. rates];
        public Task<IReadOnlyList<ShippingRate>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShippingRate>>(Items.ToArray());
        public Task SaveAllAsync(IEnumerable<ShippingRate> rates, CancellationToken cancellationToken = default)
        {
            var copy = rates.ToArray();
            Items.Clear();
            Items.AddRange(copy);
            return Task.CompletedTask;
        }
    }
}
