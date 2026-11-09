using ApexPost.Domain.Courier;

namespace ApexPost.Application.Courier;

/// <summary>
/// Persistence port for courier shipments and their scans. Like the Week 3 repositories it only stages changes;
/// <see cref="Abstractions.IUnitOfWork"/> commits them once per use case.
/// </summary>
public interface ICourierShipmentRepository
{
    /// <summary>Tracked load for a write use case.</summary>
    Task<CourierShipment?> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string trackingNumber, CancellationToken cancellationToken = default);

    void Add(CourierShipment shipment);

    /// <summary>Copies the aggregate's state onto the tracked row (rowversion-checked on commit).</summary>
    void Update(CourierShipment shipment);

    void Remove(CourierShipment shipment);

    /// <summary>Which of these scan IDs are already stored (for idempotent uploads).</summary>
    Task<IReadOnlySet<Guid>> FindExistingScanIdsAsync(IReadOnlyCollection<Guid> scanIds, CancellationToken cancellationToken = default);

    void AddScan(CourierScan scan);

    // Read side (no tracking, projections).
    Task<CourierShipmentDetails?> GetDetailsAsync(string trackingNumber, CancellationToken cancellationToken = default);

    Task<PagedResult<CourierShipmentDetails>> ListAsync(CourierShipmentQuery query, CancellationToken cancellationToken = default);

    /// <summary>Scan history ordered by scan time; null when the shipment does not exist.</summary>
    Task<IReadOnlyList<CourierScan>?> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default);
}
