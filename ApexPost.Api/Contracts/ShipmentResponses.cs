using ApexPost.Domain.Courier;

namespace ApexPost.Api.Contracts;

/// <summary>A shipment (Part B / v1 contract: flat recipient fields, numeric status code).</summary>
public sealed class ShipmentResponse
{
    /// <summary>Tracking number, e.g. APX-10000001.</summary>
    public required string TrackingNumber { get; init; }

    /// <summary>
    /// Numeric status code: 0 Created, 1 PickedUp, 2 InTransit, 3 OutForDelivery, 4 Delivered, 5 Failed.
    /// (v2 returns the status name instead.)
    /// </summary>
    public required int Status { get; init; }

    /// <summary>Weight in kilograms.</summary>
    public required decimal WeightKg { get; init; }

    /// <summary>Recipient full name.</summary>
    public required string RecipientName { get; init; }

    /// <summary>Recipient phone (E.164).</summary>
    public required string RecipientPhone { get; init; }

    /// <summary>Street address.</summary>
    public required string RecipientAddress { get; init; }

    /// <summary>Destination city.</summary>
    public required string City { get; init; }

    /// <summary>When the shipment was created (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>When the shipment last changed (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>One page of shipments.</summary>
public sealed class ShipmentPageResponse
{
    /// <summary>The shipments on this page, newest first.</summary>
    public required IReadOnlyList<ShipmentResponse> Items { get; init; }

    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Requested page size.</summary>
    public required int PageSize { get; init; }

    /// <summary>Number of shipments matching the filter.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Number of pages.</summary>
    public required int TotalPages { get; init; }
}

/// <summary>A stored scan.</summary>
public sealed class ScanResponse
{
    /// <summary>Device scan ID, scan::{Guid}.</summary>
    public required string ScanId { get; init; }

    /// <summary>Pickup, InTransit, Delivered or Failed.</summary>
    public required CourierScanType ScanType { get; init; }

    /// <summary>Courier who took the scan.</summary>
    public required string CourierId { get; init; }

    /// <summary>Where the scan happened.</summary>
    public required string Location { get; init; }

    /// <summary>When the scan happened (UTC).</summary>
    public required DateTimeOffset ScannedAtUtc { get; init; }

    /// <summary>Courier notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Signature name for a Delivered scan.</summary>
    public string? SignatureName { get; init; }

    /// <summary>When the server stored the scan (UTC).</summary>
    public required DateTimeOffset ReceivedAtUtc { get; init; }
}

/// <summary>Result of an idempotent scan upload.</summary>
public sealed class ScanUploadResponse
{
    /// <summary>New scans stored.</summary>
    public required int Accepted { get; init; }

    /// <summary>Scans whose ID was already stored (or repeated in the batch): skipped, not duplicated.</summary>
    public required int DuplicatesSkipped { get; init; }

    /// <summary>Scans refused by a business rule, with the reason.</summary>
    public required IReadOnlyList<RejectedScanResponse> Rejected { get; init; }
}

/// <summary>A scan that was not stored.</summary>
public sealed class RejectedScanResponse
{
    /// <summary>Device scan ID, scan::{Guid}.</summary>
    public required string ScanId { get; init; }

    /// <summary>Why the scan was rejected.</summary>
    public required string Reason { get; init; }
}
