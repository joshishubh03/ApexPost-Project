using ApexPost.Application.Courier;
using ApexPost.Domain.Courier;

namespace ApexPost.UnitTests.TestDoubles;

/// <summary>In-memory <see cref="ICourierShipmentRepository"/> for service unit tests and the service-level BDD scenarios (state-based fake).</summary>
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
