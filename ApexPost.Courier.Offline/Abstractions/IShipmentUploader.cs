using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Abstractions;

/// <summary>
/// Sends scans to the server. Part A uses <see cref="Outbox.FakeShipmentUploader"/>; Part D replaces it with the
/// real ApexPost API client. Implementations return one outcome per scan and throw
/// <see cref="ShipmentUploadUnavailableException"/> when the server cannot be reached at all.
/// </summary>
public interface IShipmentUploader
{
    Task<IReadOnlyList<ScanUploadOutcome>> UploadAsync(
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken cancellationToken = default);
}

/// <summary>Server verdict for one scan.</summary>
/// <param name="ScanId">Document ID of the scan.</param>
/// <param name="Result">Accepted, duplicate (already on the server) or rejected.</param>
/// <param name="Reason">Why the server rejected the scan; null otherwise.</param>
public sealed record ScanUploadOutcome(string ScanId, ScanUploadResult Result, string? Reason = null)
{
    public static ScanUploadOutcome Accepted(string scanId) => new(scanId, ScanUploadResult.Accepted);
    public static ScanUploadOutcome Duplicate(string scanId) => new(scanId, ScanUploadResult.Duplicate);
    public static ScanUploadOutcome Rejected(string scanId, string reason) => new(scanId, ScanUploadResult.Rejected, reason);
}

public enum ScanUploadResult
{
    Accepted,

    /// <summary>The server already had this scan ID - safe to treat as synced (idempotent upload).</summary>
    Duplicate,

    Rejected
}

/// <summary>
/// Transient failure (offline, timeout, server down or circuit open). The outbox keeps the scans Pending.
/// </summary>
public sealed class ShipmentUploadUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
