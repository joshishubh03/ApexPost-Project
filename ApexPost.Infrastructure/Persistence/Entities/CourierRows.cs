using ApexPost.Domain.Courier;

namespace ApexPost.Infrastructure.Persistence.Entities;

/// <summary>Week 4: a courier-tracked shipment (table CourierShipments). Additive - no Week 3 table changes.</summary>
public sealed class CourierShipmentRow
{
    public Guid Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public CourierShipmentStatus Status { get; set; }
    public decimal WeightKilograms { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string RecipientCity { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<ShipmentScanRow> Scans { get; set; } = [];
}

/// <summary>Week 4: a courier scan (table ShipmentScans). <see cref="Id"/> is the device scan GUID (idempotency key).</summary>
public sealed class ShipmentScanRow
{
    public Guid Id { get; set; }
    public Guid CourierShipmentId { get; set; }
    public CourierScanType ScanType { get; set; }
    public string CourierId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime ScannedAtUtc { get; set; }
    public string? Notes { get; set; }
    public string? SignatureName { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
