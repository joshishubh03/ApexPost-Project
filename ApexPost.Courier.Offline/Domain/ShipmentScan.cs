using System.Globalization;
using System.Text.RegularExpressions;

namespace ApexPost.Courier.Offline.Domain;

/// <summary>What happened to the parcel at the moment of the scan.</summary>
public enum ScanType
{
    Pickup,
    InTransit,
    Delivered,
    Failed
}

/// <summary>Where the scan is in the courier-device outbox.</summary>
public enum SyncStatus
{
    /// <summary>Saved locally, not yet accepted by the server.</summary>
    Pending,

    /// <summary>The server accepted the scan (or already had it).</summary>
    Synced,

    /// <summary>The server rejected the scan permanently (for example an invalid status transition).</summary>
    Failed
}

/// <summary>
/// A shipment scan recorded on the courier device. Plain C# - no Couchbase types - so the same model is used by
/// the console harness, the MAUI app and the tests. The storage adapter maps it to a JSON document.
/// </summary>
public sealed record ShipmentScan
{
    public const int MaxNotesLength = 500;
    public const int MaxLocationLength = 120;
    public const int MaxCourierIdLength = 40;
    public const int MaxSignatureLength = 120;

    public required string Id { get; init; }
    public required string TrackingNumber { get; init; }
    public required ScanType ScanType { get; init; }
    public required string CourierId { get; init; }
    public required string Location { get; init; }
    public required DateTimeOffset ScannedAtUtc { get; init; }
    public string? Notes { get; init; }

    /// <summary>Name of the person who signed for a Delivered scan (the API requires it for Delivered).</summary>
    public string? SignatureName { get; init; }

    public SyncStatus SyncStatus { get; init; } = SyncStatus.Pending;
    public int SyncAttempts { get; init; }
    public DateTimeOffset? LastSyncAttemptUtc { get; init; }
    public string? LastSyncError { get; init; }

    /// <summary>
    /// Creates a new scan that has not been uploaded yet. Every scan starts as <see cref="SyncStatus.Pending"/>:
    /// the device always saves locally first and the outbox uploads later.
    /// </summary>
    public static ShipmentScan CreatePending(
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        DateTimeOffset scannedAtUtc,
        string? notes = null,
        string? signatureName = null)
    {
        var scan = new ShipmentScan
        {
            Id = ShipmentScanId.New(),
            TrackingNumber = TrackingNumbers.Normalize(trackingNumber),
            ScanType = scanType,
            CourierId = courierId?.Trim() ?? string.Empty,
            Location = location?.Trim() ?? string.Empty,
            ScannedAtUtc = scannedAtUtc.ToUniversalTime(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            SignatureName = string.IsNullOrWhiteSpace(signatureName) ? null : signatureName.Trim(),
            SyncStatus = SyncStatus.Pending
        };
        scan.Validate();
        return scan;
    }

    /// <summary>Throws <see cref="ArgumentException"/> when the scan breaks a device-side rule.</summary>
    public void Validate()
    {
        if (!ShipmentScanId.IsValid(Id))
            throw new ArgumentException($"Scan IDs must use the format scan::{{Guid}} (got '{Id}').", nameof(Id));
        if (!TrackingNumbers.IsValid(TrackingNumber))
            throw new ArgumentException("Tracking numbers must look like APX-12345678.", nameof(TrackingNumber));
        if (!Enum.IsDefined(ScanType))
            throw new ArgumentException("Unknown scan type.", nameof(ScanType));
        if (string.IsNullOrWhiteSpace(CourierId) || CourierId.Length > MaxCourierIdLength)
            throw new ArgumentException($"Courier ID is required (max {MaxCourierIdLength} characters).", nameof(CourierId));
        if (string.IsNullOrWhiteSpace(Location) || Location.Length > MaxLocationLength)
            throw new ArgumentException($"Location is required (max {MaxLocationLength} characters).", nameof(Location));
        if (Notes is { Length: > MaxNotesLength })
            throw new ArgumentException($"Notes are limited to {MaxNotesLength} characters.", nameof(Notes));
        if (SignatureName is { Length: > MaxSignatureLength })
            throw new ArgumentException($"Signature names are limited to {MaxSignatureLength} characters.", nameof(SignatureName));
    }

    public ShipmentScan MarkSynced(DateTimeOffset attemptedAtUtc) => this with
    {
        SyncStatus = SyncStatus.Synced,
        SyncAttempts = SyncAttempts + 1,
        LastSyncAttemptUtc = attemptedAtUtc,
        LastSyncError = null
    };

    public ShipmentScan MarkFailed(DateTimeOffset attemptedAtUtc, string reason) => this with
    {
        SyncStatus = SyncStatus.Failed,
        SyncAttempts = SyncAttempts + 1,
        LastSyncAttemptUtc = attemptedAtUtc,
        LastSyncError = Truncate(reason)
    };

    /// <summary>A transient problem (no network, server down): the scan stays Pending and is retried later.</summary>
    public ShipmentScan MarkAttemptFailedTransiently(DateTimeOffset attemptedAtUtc, string reason) => this with
    {
        SyncStatus = SyncStatus.Pending,
        SyncAttempts = SyncAttempts + 1,
        LastSyncAttemptUtc = attemptedAtUtc,
        LastSyncError = Truncate(reason)
    };

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{Id} {TrackingNumber} {ScanType} @ {Location} {ScannedAtUtc:yyyy-MM-dd HH:mm:ss}Z [{SyncStatus}]");

    private static string Truncate(string reason) =>
        reason.Length <= 300 ? reason : reason[..300];
}

/// <summary>Document IDs: <c>scan::{Guid}</c>. The GUID part is what the server uses to de-duplicate uploads.</summary>
public static class ShipmentScanId
{
    public const string Prefix = "scan::";

    public static string New() => From(Guid.NewGuid());

    public static string From(Guid id) => Prefix + id.ToString("D");

    public static bool IsValid(string? id) => TryGetGuid(id, out _);

    public static bool TryGetGuid(string? id, out Guid guid)
    {
        guid = Guid.Empty;
        return id is not null &&
               id.StartsWith(Prefix, StringComparison.Ordinal) &&
               Guid.TryParseExact(id[Prefix.Length..], "D", out guid) &&
               guid != Guid.Empty;
    }
}

public static partial class TrackingNumbers
{
    public const string Pattern = @"^APX-\d{8}$";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? value) => value is not null && TrackingNumberRegex().IsMatch(value);

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex TrackingNumberRegex();
}

/// <summary>Result row of "number of scans per scan type for one courier".</summary>
public sealed record ScanTypeCount(ScanType ScanType, int Count);
