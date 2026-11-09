using Couchbase.Lite;
using Couchbase.Lite.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Offline.Storage;

/// <summary>
/// Owns the embedded Couchbase Lite database <c>apexpost_courier</c> and its default collection.
/// <list type="bullet">
/// <item>Opening the database creates the value indexes used by the SQL++ queries.</item>
/// <item>Change listeners are registered through <see cref="AddChangeListener"/>; every listener token is
/// removed in <see cref="Dispose"/> before the database is closed, so no callback can fire on a closed database.</item>
/// <item>One process owns one database file at a time (see OFFLINE-DESIGN.md): open it once and share the instance.</item>
/// </list>
/// </summary>
public sealed class CourierDatabase : IDisposable
{
    public const string DefaultName = "apexpost_courier";
    public const string TrackingNumberIndex = "idx_scans_trackingNumber";
    public const string SyncStatusIndex = "idx_scans_syncStatus";
    public const string CourierIdIndex = "idx_scans_courierId";

    private readonly Database database;
    private readonly Collection collection;
    private readonly List<ListenerToken> listenerTokens = [];
    private readonly Lock gate = new();
    private readonly ILogger logger;
    private bool disposed;

    public CourierDatabase(string directory, string name = DefaultName, ILogger<CourierDatabase>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        this.logger = logger ?? NullLogger<CourierDatabase>.Instance;
        System.IO.Directory.CreateDirectory(directory);
        Directory = directory;
        database = new Database(name, new DatabaseConfiguration { Directory = directory });
        collection = database.GetDefaultCollection();
        EnsureIndexes();
        this.logger.LogInformation("Opened Couchbase Lite database {Database} in {Directory}", name, directory);
    }

    public string Name => database.Name;

    public string Directory { get; }

    /// <summary>Number of documents in the default collection.</summary>
    public long DocumentCount
    {
        get
        {
            ThrowIfDisposed();
            return (long)collection.Count;
        }
    }

    /// <summary>The default collection (internal: only the storage adapter touches Couchbase types).</summary>
    internal Collection Collection
    {
        get
        {
            ThrowIfDisposed();
            return collection;
        }
    }

    /// <summary>Creates a SQL++ query against this database. Callers must bind values through Parameters.</summary>
    internal IQuery CreateQuery(string sqlPlusPlus)
    {
        ThrowIfDisposed();
        return database.CreateQuery(sqlPlusPlus);
    }

    /// <summary>Index names that exist on the default collection (used by tests and the harness).</summary>
    public IReadOnlyCollection<string> GetIndexNames()
    {
        ThrowIfDisposed();
        return collection.GetIndexes().ToArray();
    }

    /// <summary>
    /// Registers a collection change listener. The callback receives the IDs of the documents that changed.
    /// Disposing the returned handle removes this listener early; otherwise it is removed with the database.
    /// </summary>
    public IDisposable AddChangeListener(Action<IReadOnlyList<string>> onDocumentsChanged)
    {
        ArgumentNullException.ThrowIfNull(onDocumentsChanged);
        ThrowIfDisposed();

        var token = collection.AddChangeListener((_, change) =>
        {
            try
            {
                onDocumentsChanged(change.DocumentIDs);
            }
            catch (Exception exception)
            {
                // A faulty subscriber must never break Couchbase's notification thread.
                logger.LogError(exception, "A collection change listener failed.");
            }
        });

        lock (gate)
        {
            listenerTokens.Add(token);
        }

        return new ListenerRegistration(this, token);
    }

    /// <summary>Number of listeners still registered (0 after Dispose).</summary>
    public int ActiveListenerCount
    {
        get
        {
            lock (gate)
            {
                return listenerTokens.Count;
            }
        }
    }

    public void Dispose()
    {
        ListenerToken[] tokens;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            tokens = [.. listenerTokens];
            listenerTokens.Clear();
        }

        foreach (var token in tokens)
        {
            token.Remove();
        }

        logger.LogInformation("Removed {Count} change listener(s); closing database {Database}", tokens.Length, database.Name);
        database.Dispose(); // closes the database file
    }

    private void RemoveListener(ListenerToken token)
    {
        bool removed;
        lock (gate)
        {
            removed = !disposed && listenerTokens.Remove(token);
        }

        if (removed)
        {
            token.Remove();
        }
    }

    private void EnsureIndexes()
    {
        // Value indexes behind the SQL++ queries in CouchbaseScanRepository. CreateIndex is idempotent for
        // an identical definition, so this is safe on every start.
        collection.CreateIndex(TrackingNumberIndex, new ValueIndexConfiguration("trackingNumber"));
        collection.CreateIndex(SyncStatusIndex, new ValueIndexConfiguration("syncStatus"));
        collection.CreateIndex(CourierIdIndex, new ValueIndexConfiguration("courierId"));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed class ListenerRegistration(CourierDatabase owner, ListenerToken token) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.RemoveListener(token);
            }
        }
    }
}
