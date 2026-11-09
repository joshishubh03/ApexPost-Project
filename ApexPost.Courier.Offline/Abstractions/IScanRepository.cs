using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Abstractions;

/// <summary>
/// Local scan store used by the courier device. The contract is storage-neutral: callers never see Couchbase
/// types, so the outbox, the harness and the MAUI app can be tested against any implementation.
/// </summary>
public interface IScanRepository
{
    /// <summary>Saves a new scan. Throws <see cref="InvalidOperationException"/> if the ID already exists.</summary>
    Task<ShipmentScan> CreateAsync(ShipmentScan scan, CancellationToken cancellationToken = default);

    /// <summary>Key-value lookup by document ID (<c>scan::{Guid}</c>). Returns null when not found.</summary>
    Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Replaces an existing scan. Throws <see cref="KeyNotFoundException"/> when it does not exist.</summary>
    Task<ShipmentScan> UpdateAsync(ShipmentScan scan, CancellationToken cancellationToken = default);

    /// <summary>Deletes a scan. Returns false when it did not exist.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>SQL++: all scans for one tracking number, oldest first (ordered by scannedAtUtc).</summary>
    Task<IReadOnlyList<ShipmentScan>> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);

    /// <summary>SQL++: scans with syncStatus = 'Pending', oldest first; <paramref name="limit"/> caps a batch.</summary>
    Task<IReadOnlyList<ShipmentScan>> ListPendingAsync(int? limit = null, CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>SQL++: number of scans per scan type for one courier.</summary>
    Task<IReadOnlyList<ScanTypeCount>> CountByScanTypeAsync(string courierId, CancellationToken cancellationToken = default);
}
