using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Offline.Monitoring;

/// <summary>
/// Collection change listener for the courier store: logs the IDs of the documents that changed and keeps a live
/// "pending scans" count (raised through <see cref="PendingCountChanged"/>; the harness prints it, the MAUI app
/// binds it to the Pending Scans screen). The listener token is owned by <see cref="CourierDatabase"/> and removed
/// when either this monitor or the database is disposed.
/// </summary>
public sealed class PendingScanMonitor : IDisposable
{
    private readonly IScanRepository repository;
    private readonly ILogger log;
    private readonly IDisposable registration;
    private int pendingCount;

    public PendingScanMonitor(CourierDatabase database, IScanRepository repository, ILogger<PendingScanMonitor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        log = logger ?? NullLogger<PendingScanMonitor>.Instance;
        pendingCount = repository.CountPendingAsync().GetAwaiter().GetResult();
        registration = database.AddChangeListener(OnDocumentsChanged);
    }

    /// <summary>Raised on Couchbase's notification thread after every change: (changed document IDs, pending count).</summary>
    public event EventHandler<PendingScansChangedEventArgs>? PendingCountChanged;

    public int PendingCount => Volatile.Read(ref pendingCount);

    public void Dispose() => registration.Dispose();

    private void OnDocumentsChanged(IReadOnlyList<string> documentIds)
    {
        var count = repository.CountPendingAsync().GetAwaiter().GetResult();
        Volatile.Write(ref pendingCount, count);
        log.LogInformation("[listener] {Count} document(s) changed: {DocumentIds} | pending scans: {Pending}",
            documentIds.Count, string.Join(", ", documentIds), count);
        PendingCountChanged?.Invoke(this, new PendingScansChangedEventArgs(documentIds, count));
    }
}

public sealed class PendingScansChangedEventArgs(IReadOnlyList<string> changedDocumentIds, int pendingCount) : EventArgs
{
    public IReadOnlyList<string> ChangedDocumentIds { get; } = changedDocumentIds;
    public int PendingCount { get; } = pendingCount;
}
