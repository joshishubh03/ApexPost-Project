using ApexPost.Application.Courier;
using ApexPost.Domain.Courier;

namespace ApexPost.Api.Contracts;

/// <summary>
/// A shipment (v2). Breaking changes from v1: recipient fields are nested in <see cref="Recipient"/>, a
/// <see cref="LatestScan"/> summary is added and <see cref="Status"/> is a string enum.
/// </summary>
public sealed class ShipmentResponseV2
{
    /// <summary>Tracking number, e.g. APX-10000001.</summary>
    public required string TrackingNumber { get; init; }

    /// <summary>Status name: Created, PickedUp, InTransit, OutForDelivery, Delivered or Failed.</summary>
    public required CourierShipmentStatus Status { get; init; }

    /// <summary>Weight in kilograms.</summary>
    public required decimal WeightKg { get; init; }

    /// <summary>Who receives the parcel.</summary>
    public required RecipientResponse Recipient { get; init; }

    /// <summary>The most recent scan, or null before the first scan.</summary>
    public LatestScanResponse? LatestScan { get; init; }

    /// <summary>When the shipment was created (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>When the shipment last changed (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>Recipient details (v2).</summary>
public sealed class RecipientResponse
{
    /// <summary>Full name.</summary>
    public required string Name { get; init; }

    /// <summary>Phone number (E.164).</summary>
    public required string Phone { get; init; }

    /// <summary>Street address.</summary>
    public required string Address { get; init; }

    /// <summary>City.</summary>
    public required string City { get; init; }
}

/// <summary>Summary of the latest scan (v2).</summary>
public sealed class LatestScanResponse
{
    /// <summary>Pickup, InTransit, Delivered or Failed.</summary>
    public required CourierScanType ScanType { get; init; }

    /// <summary>Where the scan happened.</summary>
    public required string Location { get; init; }

    /// <summary>When the scan happened (UTC).</summary>
    public required DateTimeOffset ScannedAtUtc { get; init; }
}

/// <summary>One page of v2 shipments.</summary>
public sealed class ShipmentPageResponseV2
{
    /// <summary>The shipments on this page, newest first.</summary>
    public required IReadOnlyList<ShipmentResponseV2> Items { get; init; }

    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Requested page size.</summary>
    public required int PageSize { get; init; }

    /// <summary>Number of shipments matching the filter.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Number of pages.</summary>
    public required int TotalPages { get; init; }
}

/// <summary>v2 mapping (same service read model as v1; only the DTO shape differs).</summary>
public static class ContractMappingsV2
{
    public static ShipmentResponseV2 ToResponseV2(this CourierShipmentDetails shipment) => new()
    {
        TrackingNumber = shipment.TrackingNumber,
        Status = shipment.Status,
        WeightKg = shipment.WeightKilograms,
        Recipient = new RecipientResponse
        {
            Name = shipment.Recipient.Name,
            Phone = shipment.Recipient.Phone,
            Address = shipment.Recipient.Address,
            City = shipment.Recipient.City
        },
        LatestScan = shipment.LatestScan is { } latest
            ? new LatestScanResponse { ScanType = latest.ScanType, Location = latest.Location, ScannedAtUtc = latest.ScannedAtUtc }
            : null,
        CreatedAtUtc = shipment.CreatedAtUtc,
        UpdatedAtUtc = shipment.UpdatedAtUtc
    };

    public static ShipmentPageResponseV2 ToResponseV2(this PagedResult<CourierShipmentDetails> page) => new()
    {
        Items = page.Items.Select(item => item.ToResponseV2()).ToArray(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalCount = page.TotalCount,
        TotalPages = page.TotalPages
    };
}
