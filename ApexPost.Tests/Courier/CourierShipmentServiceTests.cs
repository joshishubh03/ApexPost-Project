using ApexPost.Application.Courier;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Courier;

namespace ApexPost.Tests.Courier;

/// <summary>Service rules with an in-memory repository (no database).</summary>
public sealed class CourierShipmentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
    private readonly InMemoryCourierShipmentRepository repository = new();
    private readonly CourierShipmentService service;

    public CourierShipmentServiceTests() =>
        service = new CourierShipmentService(repository, ImmediateUnitOfWork.Instance, new FixedClock(Now));

    [Fact]
    public async Task Creating_a_duplicate_tracking_number_is_a_DuplicateResourceException()
    {
        await service.CreateAsync(Command("APX-10000001"));

        await Assert.ThrowsAsync<DuplicateResourceException>(() => service.CreateAsync(Command("apx-10000001")));
    }

    [Fact]
    public async Task Unknown_tracking_numbers_throw_ShipmentNotFoundException()
    {
        await Assert.ThrowsAsync<ShipmentNotFoundException>(() => service.GetAsync("APX-10000009"));
        await Assert.ThrowsAsync<ShipmentNotFoundException>(() => service.DeleteAsync("APX-10000009"));
    }

    [Fact]
    public async Task Delete_after_pickup_is_refused()
    {
        await service.CreateAsync(Command("APX-10000001"));
        await service.ChangeStatusAsync("APX-10000001", CourierShipmentStatus.PickedUp);

        await Assert.ThrowsAsync<ShipmentRuleViolationException>(() => service.DeleteAsync("APX-10000001"));
    }

    [Fact]
    public async Task Upload_is_idempotent_and_reports_accepted_duplicates_and_rejections()
    {
        await service.CreateAsync(Command("APX-10000001"));
        var pickup = Item(Guid.NewGuid(), CourierScanType.Pickup, Now.AddMinutes(-30));
        var delivered = Item(Guid.NewGuid(), CourierScanType.Delivered, Now.AddMinutes(-10), "J. Doe");

        var first = await service.UploadScansAsync("APX-10000001", [pickup, pickup, delivered]);
        var second = await service.UploadScansAsync("APX-10000001", [pickup]);

        Assert.Equal(1, first.Accepted);
        Assert.Equal(1, first.DuplicatesSkipped);
        Assert.Equal(delivered.ScanId, Assert.Single(first.Rejected).ScanId);
        Assert.Equal(0, second.Accepted);
        Assert.Equal(1, second.DuplicatesSkipped);
        Assert.Single(await service.GetScanHistoryAsync("APX-10000001"));
        Assert.Equal(CourierShipmentStatus.PickedUp, (await service.GetAsync("APX-10000001")).Status);
    }

    [Fact]
    public async Task Couriers_cannot_upload_under_another_courier_id()
    {
        await service.CreateAsync(Command("APX-10000001"));

        await Assert.ThrowsAsync<ScanOwnershipException>(() => service.UploadScansAsync(
            "APX-10000001", [Item(Guid.NewGuid(), CourierScanType.Pickup, Now)], authenticatedCourierId: "courier-999"));
    }

    [Fact]
    public async Task Page_size_above_the_maximum_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(new CourierShipmentQuery(1, 51, null, null)));
    }

    private static CreateCourierShipmentCommand Command(string trackingNumber) =>
        new(trackingNumber, 2.5m, CourierShipmentDomainTests.Recipient());

    private static ScanUploadItem Item(Guid id, CourierScanType type, DateTimeOffset at, string? signature = null) =>
        new(id, type, "courier-007", "Maplewood Depot", at, null, signature);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

/// <summary>In-memory <see cref="ICourierShipmentRepository"/> for service unit tests.</summary>
internal sealed class InMemoryCourierShipmentRepository : ICourierShipmentRepository
{
    private readonly Dictionary<string, CourierShipment> shipments = new(StringComparer.Ordinal);
    private readonly List<CourierScan> scans = [];

    public Task<CourierShipment?> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(shipments.GetValueOrDefault(trackingNumber));

    public Task<bool> ExistsAsync(string trackingNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(shipments.ContainsKey(trackingNumber));

    public void Add(CourierShipment shipment) => shipments.Add(shipment.TrackingNumber, shipment);

    public void Update(CourierShipment shipment) => shipments[shipment.TrackingNumber] = shipment;

    public void Remove(CourierShipment shipment) => shipments.Remove(shipment.TrackingNumber);

    public Task<IReadOnlySet<Guid>> FindExistingScanIdsAsync(IReadOnlyCollection<Guid> scanIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(scans.Select(scan => scan.Id).Where(id => scanIds.Contains(id)).ToHashSet());

    public void AddScan(CourierScan scan) => scans.Add(scan);

    public Task<CourierShipmentDetails?> GetDetailsAsync(string trackingNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(shipments.TryGetValue(trackingNumber, out var shipment) ? Details(shipment) : null);

    public Task<PagedResult<CourierShipmentDetails>> ListAsync(CourierShipmentQuery query, CancellationToken cancellationToken = default)
    {
        var matches = shipments.Values
            .Where(shipment => query.Status is null || shipment.Status == query.Status)
            .Where(shipment => query.City is null || shipment.Recipient.City == query.City)
            .ToArray();
        var page = matches.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(Details).ToArray();
        return Task.FromResult(new PagedResult<CourierShipmentDetails>(page, query.Page, query.PageSize, matches.Length));
    }

    public Task<IReadOnlyList<CourierScan>?> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CourierScan>?>(shipments.TryGetValue(trackingNumber, out var shipment)
            ? scans.Where(scan => scan.ShipmentId == shipment.Id).OrderBy(scan => scan.ScannedAtUtc).ToArray()
            : null);

    private CourierShipmentDetails Details(CourierShipment shipment)
    {
        var latest = scans.Where(scan => scan.ShipmentId == shipment.Id).MaxBy(scan => scan.ScannedAtUtc);
        return new CourierShipmentDetails(
            shipment.TrackingNumber, shipment.Status, shipment.WeightKilograms, shipment.Recipient,
            shipment.CreatedAtUtc, shipment.UpdatedAtUtc,
            latest is null ? null : new LatestScanSummary(latest.ScanType, latest.Location, latest.ScannedAtUtc));
    }
}
