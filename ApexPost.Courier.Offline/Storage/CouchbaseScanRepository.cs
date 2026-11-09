using System.Globalization;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using Couchbase.Lite;
using Couchbase.Lite.Query;

namespace ApexPost.Courier.Offline.Storage;

/// <summary>
/// Couchbase Lite implementation of <see cref="IScanRepository"/>. Scans are JSON documents in the default
/// collection with ID <c>scan::{Guid}</c>. Key-value operations use the document ID directly; list/filter
/// operations use SQL++ with named parameters only (no value is ever concatenated into query text).
/// </summary>
public sealed class CouchbaseScanRepository(CourierDatabase database) : IScanRepository
{
    /// <summary>Discriminator stored on every scan document, so other document types can share the collection.</summary>
    public const string DocumentType = "shipmentScan";

    // SQL++ text is constant. Values are bound with Parameters ($docType, $trackingNumber, ...).
    private const string SelectScan =
        "SELECT META().id AS id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, " +
        "signatureName, syncStatus, syncAttempts, lastSyncAttemptUtc, lastSyncError FROM _ ";

    internal const string ByTrackingNumberQuery =
        SelectScan + "WHERE docType = $docType AND trackingNumber = $trackingNumber ORDER BY scannedAtUtc, META().id";

    internal const string PendingQuery =
        SelectScan + "WHERE docType = $docType AND syncStatus = $syncStatus ORDER BY scannedAtUtc, META().id";

    internal const string PendingWithLimitQuery =
        SelectScan + "WHERE docType = $docType AND syncStatus = $syncStatus ORDER BY scannedAtUtc, META().id LIMIT $limit";

    internal const string AllQuery =
        SelectScan + "WHERE docType = $docType ORDER BY scannedAtUtc, META().id";

    internal const string CountPendingQuery =
        "SELECT COUNT(*) AS total FROM _ WHERE docType = $docType AND syncStatus = $syncStatus";

    internal const string CountByScanTypeQuery =
        "SELECT scanType, COUNT(*) AS total FROM _ WHERE docType = $docType AND courierId = $courierId " +
        "GROUP BY scanType ORDER BY scanType";

    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public Task<ShipmentScan> CreateAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);
        cancellationToken.ThrowIfCancellationRequested();
        scan.Validate();

        using var document = new MutableDocument(scan.Id);
        Write(document, scan);

        // FailOnConflict makes "create" atomic: saving a new document over an existing ID is rejected.
        if (!database.Collection.Save(document, ConcurrencyControl.FailOnConflict))
        {
            throw new InvalidOperationException($"A scan with ID {scan.Id} already exists.");
        }

        return Task.FromResult(scan);
    }

    public Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ShipmentScanId.IsValid(id))
        {
            return Task.FromResult<ShipmentScan?>(null);
        }

        using var document = database.Collection.GetDocument(id);
        return Task.FromResult(document is null || document.GetString("docType") != DocumentType
            ? null
            : Read(document, document.Id));
    }

    public Task<ShipmentScan> UpdateAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);
        cancellationToken.ThrowIfCancellationRequested();
        scan.Validate();

        using var existing = database.Collection.GetDocument(scan.Id)
            ?? throw new KeyNotFoundException($"Scan {scan.Id} does not exist.");
        using var document = existing.ToMutable();
        Write(document, scan);

        // On a single device the newest local write wins (the server decides between devices - see OFFLINE-DESIGN.md).
        database.Collection.Save(document, ConcurrencyControl.LastWriteWins);
        return Task.FromResult(scan);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ShipmentScanId.IsValid(id))
        {
            return Task.FromResult(false);
        }

        using var document = database.Collection.GetDocument(id);
        if (document is null)
        {
            return Task.FromResult(false);
        }

        database.Collection.Delete(document);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken cancellationToken = default) =>
        RunScanQuery(AllQuery, parameters => parameters, cancellationToken);

    public Task<IReadOnlyList<ShipmentScan>> FindByTrackingNumberAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        var normalized = TrackingNumbers.Normalize(trackingNumber);
        return RunScanQuery(
            ByTrackingNumberQuery,
            parameters => parameters.SetString("trackingNumber", normalized),
            cancellationToken);
    }

    public Task<IReadOnlyList<ShipmentScan>> ListPendingAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        if (limit is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(limit));
            return RunScanQuery(
                PendingWithLimitQuery,
                parameters => parameters
                    .SetString("syncStatus", nameof(SyncStatus.Pending))
                    .SetInt("limit", value),
                cancellationToken);
        }

        return RunScanQuery(
            PendingQuery,
            parameters => parameters.SetString("syncStatus", nameof(SyncStatus.Pending)),
            cancellationToken);
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var query = database.CreateQuery(CountPendingQuery);
        query.Parameters = new Parameters()
            .SetString("docType", DocumentType)
            .SetString("syncStatus", nameof(SyncStatus.Pending));

        using var results = query.Execute();
        var total = results.Select(row => row.GetInt("total")).FirstOrDefault();
        return Task.FromResult(total);
    }

    public Task<IReadOnlyList<ScanTypeCount>> CountByScanTypeAsync(string courierId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courierId);
        cancellationToken.ThrowIfCancellationRequested();

        using var query = database.CreateQuery(CountByScanTypeQuery);
        query.Parameters = new Parameters()
            .SetString("docType", DocumentType)
            .SetString("courierId", courierId.Trim());

        using var results = query.Execute();
        var counts = new List<ScanTypeCount>();
        foreach (var row in results)
        {
            if (Enum.TryParse<ScanType>(row.GetString("scanType"), ignoreCase: true, out var scanType))
            {
                counts.Add(new ScanTypeCount(scanType, row.GetInt("total")));
            }
        }

        return Task.FromResult<IReadOnlyList<ScanTypeCount>>(counts);
    }

    /// <summary>The SQLite plan Couchbase Lite will use (shows whether a value index is used). For the harness.</summary>
    public string ExplainTrackingNumberQuery(string trackingNumber)
    {
        using var query = database.CreateQuery(ByTrackingNumberQuery);
        query.Parameters = new Parameters()
            .SetString("docType", DocumentType)
            .SetString("trackingNumber", TrackingNumbers.Normalize(trackingNumber));
        return query.Explain();
    }

    /// <summary>The plan for the pending-scans query (uses the syncStatus value index).</summary>
    public string ExplainPendingQuery()
    {
        using var query = database.CreateQuery(PendingQuery);
        query.Parameters = new Parameters()
            .SetString("docType", DocumentType)
            .SetString("syncStatus", nameof(SyncStatus.Pending));
        return query.Explain();
    }

    private Task<IReadOnlyList<ShipmentScan>> RunScanQuery(
        string sqlPlusPlus,
        Func<Parameters, Parameters> bind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var query = database.CreateQuery(sqlPlusPlus);
        query.Parameters = bind(new Parameters().SetString("docType", DocumentType));

        using var results = query.Execute();
        var scans = new List<ShipmentScan>();
        foreach (var row in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = row.GetString("id");
            if (id is not null)
            {
                scans.Add(Read(row, id));
            }
        }

        return Task.FromResult<IReadOnlyList<ShipmentScan>>(scans);
    }

    private static void Write(MutableDocument document, ShipmentScan scan)
    {
        document
            .SetString("docType", DocumentType)
            .SetString("trackingNumber", scan.TrackingNumber)
            .SetString("scanType", scan.ScanType.ToString())
            .SetString("courierId", scan.CourierId)
            .SetString("location", scan.Location)
            .SetString("scannedAtUtc", FormatDate(scan.ScannedAtUtc))
            .SetString("notes", scan.Notes)
            .SetString("signatureName", scan.SignatureName)
            .SetString("syncStatus", scan.SyncStatus.ToString())
            .SetInt("syncAttempts", scan.SyncAttempts)
            .SetString("lastSyncAttemptUtc", scan.LastSyncAttemptUtc is { } attempted ? FormatDate(attempted) : null)
            .SetString("lastSyncError", scan.LastSyncError);
    }

    private static ShipmentScan Read(IDictionaryObject values, string id) => new()
    {
        Id = id,
        TrackingNumber = values.GetString("trackingNumber") ?? string.Empty,
        ScanType = ParseEnum<ScanType>(values.GetString("scanType"), id),
        CourierId = values.GetString("courierId") ?? string.Empty,
        Location = values.GetString("location") ?? string.Empty,
        ScannedAtUtc = ParseDate(values.GetString("scannedAtUtc"), id),
        Notes = values.GetString("notes"),
        SignatureName = values.GetString("signatureName"),
        SyncStatus = ParseEnum<SyncStatus>(values.GetString("syncStatus"), id),
        SyncAttempts = values.GetInt("syncAttempts"),
        LastSyncAttemptUtc = values.GetString("lastSyncAttemptUtc") is { Length: > 0 } attempted
            ? ParseDate(attempted, id)
            : null,
        LastSyncError = values.GetString("lastSyncError")
    };

    // Fixed-width UTC ISO-8601 strings sort lexicographically in time order, so ORDER BY scannedAtUtc is correct.
    private static string FormatDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string? value, string id) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Scan {id} has an invalid date '{value}'.");

    private static TEnum ParseEnum<TEnum>(string? value, string id)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Scan {id} has an invalid {typeof(TEnum).Name} '{value}'.");
}
