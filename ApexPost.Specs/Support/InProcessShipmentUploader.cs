using ApexPost.Application.Courier;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Domain.Courier;

namespace ApexPost.Specs.Support;

/// <summary>
/// Connects the device outbox to the server's <see cref="CourierShipmentService"/> in-process (no HTTP). It maps
/// the server's answer back to one outcome per scan, the same way the app's real uploader does: rejected scans
/// carry the server's reason; accepted and duplicate scans both count as synced.
/// </summary>
public sealed class InProcessShipmentUploader(CourierShipmentService server) : IShipmentUploader
{
    /// <summary>False = no network: every upload fails as "unavailable", so the outbox keeps scans Pending.</summary>
    public bool IsOnline { get; set; }

    /// <summary>Duplicates the server reported across all uploads in this scenario.</summary>
    public int DuplicatesSkipped { get; private set; }

    public async Task<IReadOnlyList<ScanUploadOutcome>> UploadAsync(
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scans);
        if (!IsOnline)
        {
            throw new ShipmentUploadUnavailableException("No network connection.");
        }

        var outcomes = new List<ScanUploadOutcome>(scans.Count);
        foreach (var parcel in scans.GroupBy(scan => scan.TrackingNumber, StringComparer.Ordinal))
        {
            var summary = await server.UploadScansAsync(
                parcel.Key, parcel.Select(ToUploadItem).ToList(), authenticatedCourierId: null, cancellationToken);
            DuplicatesSkipped += summary.DuplicatesSkipped;

            var rejected = summary.Rejected.ToDictionary(item => item.ScanId, item => item.Reason);
            foreach (var scan in parcel)
            {
                outcomes.Add(rejected.TryGetValue(ServerId(scan), out var reason)
                    ? ScanUploadOutcome.Rejected(scan.Id, reason)
                    : ScanUploadOutcome.Accepted(scan.Id));
            }
        }

        return outcomes;
    }

    /// <summary>The device scan as the server's upload item (the GUID part of scan::{Guid} is the server ID).</summary>
    public static ScanUploadItem ToUploadItem(ShipmentScan scan) => new(
        ServerId(scan),
        Enum.Parse<CourierScanType>(scan.ScanType.ToString()),
        scan.CourierId,
        scan.Location,
        scan.ScannedAtUtc,
        scan.Notes,
        scan.SignatureName);

    private static Guid ServerId(ShipmentScan scan) =>
        ShipmentScanId.TryGetGuid(scan.Id, out var id)
            ? id
            : throw new InvalidOperationException($"Scan {scan.Id} does not use the scan::{{Guid}} format.");
}
