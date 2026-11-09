using ApexPost.Application.Courier;
using ApexPost.Domain.Courier;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core adapter for <see cref="ICourierShipmentRepository"/>. Writes only stage changes on the scoped
/// <see cref="ApexPostDbContext"/> (the unit of work commits); reads are AsNoTracking projections.
/// </summary>
public sealed class EfCourierShipmentRepository(ApexPostDbContext context) : ICourierShipmentRepository
{
    public async Task<CourierShipment?> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var row = await context.CourierShipments
            .SingleOrDefaultAsync(shipment => shipment.TrackingNumber == trackingNumber, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public Task<bool> ExistsAsync(string trackingNumber, CancellationToken cancellationToken = default) =>
        context.CourierShipments.AnyAsync(shipment => shipment.TrackingNumber == trackingNumber, cancellationToken);

    public void Add(CourierShipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        var row = new CourierShipmentRow { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber };
        Apply(row, shipment);
        row.CreatedAtUtc = shipment.CreatedAtUtc.UtcDateTime;
        context.CourierShipments.Add(row);
    }

    public void Update(CourierShipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        Apply(TrackedRow(shipment.Id), shipment);
    }

    public void Remove(CourierShipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        // Scans are removed by the ON DELETE CASCADE foreign key.
        context.CourierShipments.Remove(TrackedRow(shipment.Id));
    }

    public async Task<IReadOnlySet<Guid>> FindExistingScanIdsAsync(IReadOnlyCollection<Guid> scanIds, CancellationToken cancellationToken = default)
    {
        if (scanIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = scanIds.ToArray();
        var existing = await context.ShipmentScans
            .AsNoTracking()
            .Where(scan => ids.Contains(scan.Id))
            .Select(scan => scan.Id)
            .ToListAsync(cancellationToken);
        return existing.ToHashSet();
    }

    public void AddScan(CourierScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        context.ShipmentScans.Add(new ShipmentScanRow
        {
            Id = scan.Id,
            CourierShipmentId = scan.ShipmentId,
            ScanType = scan.ScanType,
            CourierId = scan.CourierId,
            Location = scan.Location,
            ScannedAtUtc = scan.ScannedAtUtc.UtcDateTime,
            Notes = scan.Notes,
            SignatureName = scan.SignatureName,
            ReceivedAtUtc = scan.ReceivedAtUtc.UtcDateTime
        });
    }

    public async Task<CourierShipmentDetails?> GetDetailsAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var projection = await Project(context.CourierShipments.AsNoTracking()
                .Where(shipment => shipment.TrackingNumber == trackingNumber))
            .SingleOrDefaultAsync(cancellationToken);
        return projection is null ? null : ToDetails(projection);
    }

    public async Task<PagedResult<CourierShipmentDetails>> ListAsync(CourierShipmentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var shipments = context.CourierShipments.AsNoTracking();
        if (query.Status is { } status)
        {
            shipments = shipments.Where(shipment => shipment.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City;
            shipments = shipments.Where(shipment => shipment.RecipientCity == city);
        }

        var total = await shipments.CountAsync(cancellationToken);
        var page = await Project(shipments
                .OrderByDescending(shipment => shipment.CreatedAtUtc)
                .ThenBy(shipment => shipment.TrackingNumber)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<CourierShipmentDetails>(page.Select(ToDetails).ToArray(), query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<CourierScan>?> GetScanHistoryAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var shipmentId = await context.CourierShipments.AsNoTracking()
            .Where(shipment => shipment.TrackingNumber == trackingNumber)
            .Select(shipment => (Guid?)shipment.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (shipmentId is null)
        {
            return null;
        }

        var rows = await context.ShipmentScans.AsNoTracking()
            .Where(scan => scan.CourierShipmentId == shipmentId.Value)
            .OrderBy(scan => scan.ScannedAtUtc)
            .ThenBy(scan => scan.ReceivedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new CourierScan(
                row.Id,
                row.CourierShipmentId,
                row.ScanType,
                row.CourierId,
                row.Location,
                Utc(row.ScannedAtUtc),
                row.Notes,
                row.SignatureName,
                Utc(row.ReceivedAtUtc)))
            .ToArray();
    }

    private CourierShipmentRow TrackedRow(Guid id) =>
        context.CourierShipments.Local.SingleOrDefault(row => row.Id == id)
        ?? throw new InvalidOperationException("Load the shipment in the current unit of work before changing it.");

    private static void Apply(CourierShipmentRow row, CourierShipment shipment)
    {
        row.Status = shipment.Status;
        row.WeightKilograms = shipment.WeightKilograms;
        row.RecipientName = shipment.Recipient.Name;
        row.RecipientPhone = shipment.Recipient.Phone;
        row.RecipientAddress = shipment.Recipient.Address;
        row.RecipientCity = shipment.Recipient.City;
        row.UpdatedAtUtc = shipment.UpdatedAtUtc.UtcDateTime;
    }

    private static CourierShipment ToDomain(CourierShipmentRow row) => new(
        row.Id,
        row.TrackingNumber,
        row.WeightKilograms,
        new Recipient(row.RecipientName, row.RecipientPhone, row.RecipientAddress, row.RecipientCity),
        row.Status,
        Utc(row.CreatedAtUtc),
        Utc(row.UpdatedAtUtc));

    private static IQueryable<ShipmentProjection> Project(IQueryable<CourierShipmentRow> shipments) =>
        shipments.Select(shipment => new ShipmentProjection
        {
            TrackingNumber = shipment.TrackingNumber,
            Status = shipment.Status,
            WeightKilograms = shipment.WeightKilograms,
            RecipientName = shipment.RecipientName,
            RecipientPhone = shipment.RecipientPhone,
            RecipientAddress = shipment.RecipientAddress,
            RecipientCity = shipment.RecipientCity,
            CreatedAtUtc = shipment.CreatedAtUtc,
            UpdatedAtUtc = shipment.UpdatedAtUtc,
            LatestScan = shipment.Scans
                .OrderByDescending(scan => scan.ScannedAtUtc)
                .ThenByDescending(scan => scan.ReceivedAtUtc)
                .Select(scan => new LatestScanProjection
                {
                    ScanType = scan.ScanType,
                    Location = scan.Location,
                    ScannedAtUtc = scan.ScannedAtUtc
                })
                .FirstOrDefault()
        });

    private static CourierShipmentDetails ToDetails(ShipmentProjection projection) => new(
        projection.TrackingNumber,
        projection.Status,
        projection.WeightKilograms,
        new Recipient(projection.RecipientName, projection.RecipientPhone, projection.RecipientAddress, projection.RecipientCity),
        Utc(projection.CreatedAtUtc),
        Utc(projection.UpdatedAtUtc),
        projection.LatestScan is { } latest
            ? new LatestScanSummary(latest.ScanType, latest.Location, Utc(latest.ScannedAtUtc))
            : null);

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class ShipmentProjection
    {
        public string TrackingNumber { get; init; } = string.Empty;
        public CourierShipmentStatus Status { get; init; }
        public decimal WeightKilograms { get; init; }
        public string RecipientName { get; init; } = string.Empty;
        public string RecipientPhone { get; init; } = string.Empty;
        public string RecipientAddress { get; init; } = string.Empty;
        public string RecipientCity { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public LatestScanProjection? LatestScan { get; init; }
    }

    private sealed class LatestScanProjection
    {
        public CourierScanType ScanType { get; init; }
        public string Location { get; init; } = string.Empty;
        public DateTime ScannedAtUtc { get; init; }
    }
}
