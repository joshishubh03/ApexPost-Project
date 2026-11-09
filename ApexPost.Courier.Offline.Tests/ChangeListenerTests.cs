using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Monitoring;
using ApexPost.Courier.Offline.Storage;

namespace ApexPost.Courier.Offline.Tests;

public sealed class ChangeListenerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Listener_reports_changed_document_ids_and_the_live_pending_count()
    {
        using var store = new TemporaryCourierDatabase();
        using var monitor = new PendingScanMonitor(store.Database, store.Repository);
        var notified = new TaskCompletionSource<PendingScansChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.PendingCountChanged += (_, change) => notified.TrySetResult(change);

        var scan = await store.Repository.CreateAsync(
            ShipmentScan.CreatePending("APX-10000001", ScanType.Pickup, "courier-007", "Depot", DateTimeOffset.UtcNow));

        var change = await notified.Task.WaitAsync(Timeout);
        Assert.Contains(scan.Id, change.ChangedDocumentIds);
        Assert.Equal(1, change.PendingCount);
        Assert.Equal(1, monitor.PendingCount);
    }

    [Fact]
    public void Disposing_the_database_removes_every_listener_token()
    {
        var directory = Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));
        var database = new CourierDatabase(directory);
        var repository = new CouchbaseScanRepository(database);
        _ = new PendingScanMonitor(database, repository);
        _ = database.AddChangeListener(_ => { });
        Assert.Equal(2, database.ActiveListenerCount);

        database.Dispose();

        Assert.Equal(0, database.ActiveListenerCount);
        Assert.Throws<ObjectDisposedException>(() => database.DocumentCount);
        TryDelete(directory);
    }

    [Fact]
    public void Disposing_a_registration_removes_only_that_listener()
    {
        using var store = new TemporaryCourierDatabase();
        var first = store.Database.AddChangeListener(_ => { });
        using var second = store.Database.AddChangeListener(_ => { });

        first.Dispose();
        first.Dispose();

        Assert.Equal(1, store.Database.ActiveListenerCount);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
