using ApexPost.Domain.Courier;

namespace ApexPost.Application.Courier;

/// <summary>Read model returned by the courier shipment service (API DTOs are mapped from it per version).</summary>
public sealed record CourierShipmentDetails(
    string TrackingNumber,
    CourierShipmentStatus Status,
    decimal WeightKilograms,
    Recipient Recipient,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    LatestScanSummary? LatestScan);

public sealed record LatestScanSummary(CourierScanType ScanType, string Location, DateTimeOffset ScannedAtUtc);

public sealed record CourierShipmentQuery(int Page, int PageSize, CourierShipmentStatus? Status, string? City)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record CreateCourierShipmentCommand(string TrackingNumber, decimal WeightKilograms, Recipient Recipient);

public sealed record ReplaceCourierShipmentCommand(decimal WeightKilograms, Recipient Recipient);

/// <summary>One offline scan in an upload batch. <see cref="ScanId"/> is the GUID from the device's scan::{Guid} ID.</summary>
public sealed record ScanUploadItem(
    Guid ScanId,
    CourierScanType ScanType,
    string CourierId,
    string Location,
    DateTimeOffset ScannedAtUtc,
    string? Notes,
    string? SignatureName);

public sealed record RejectedScan(Guid ScanId, string Reason);

/// <summary>Result of an idempotent scan upload.</summary>
public sealed record ScanUploadSummary(int Accepted, int DuplicatesSkipped, IReadOnlyList<RejectedScan> Rejected);

/// <summary>The tracking number does not exist (HTTP 404).</summary>
public sealed class ShipmentNotFoundException(string trackingNumber)
    : KeyNotFoundException($"No shipment with tracking number {trackingNumber} exists.")
{
    public string TrackingNumber { get; } = trackingNumber;
}
