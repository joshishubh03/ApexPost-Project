using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Outbox;

namespace ApexPost.Courier.Offline.Tests;

public sealed class OutboxServiceTests : IDisposable
{
    private readonly TemporaryCourierDatabase store = new();
    private readonly FakeShipmentUploader uploader = new();

    public void Dispose() => store.Dispose();

    [Fact]
    public async Task Recorded_scans_are_saved_locally_first_as_pending_without_calling_the_server()
    {
        var outbox = new OutboxService(store.Repository, uploader);

        var scan = await outbox.RecordScanAsync("APX-10000001", ScanType.Pickup, "courier-007", "Maplewood Depot");

        Assert.Equal(SyncStatus.Pending, (await store.Repository.GetByIdAsync(scan.Id))!.SyncStatus);
        Assert.Equal(0, uploader.UploadCalls);
    }

    [Fact]
    public async Task Sync_marks_accepted_scans_synced_and_rejected_scans_failed()
    {
        var outbox = new OutboxService(store.Repository, uploader);
        var accepted = await outbox.RecordScanAsync("APX-10000001", ScanType.Pickup, "courier-007", "Depot");
        var rejected = await outbox.RecordScanAsync("APX-10000002", ScanType.Pickup, "courier-007", "Depot", notes: "#reject");

        var result = await outbox.SyncPendingAsync();

        Assert.Equal(1, result.Synced);
        Assert.Equal(1, result.Failed);
        Assert.Equal(SyncStatus.Synced, (await store.Repository.GetByIdAsync(accepted.Id))!.SyncStatus);
        var failed = (await store.Repository.GetByIdAsync(rejected.Id))!;
        Assert.Equal(SyncStatus.Failed, failed.SyncStatus);
        Assert.Contains("#reject", failed.LastSyncError, StringComparison.Ordinal);
        Assert.Equal(0, await store.Repository.CountPendingAsync());
    }

    [Fact]
    public async Task Offline_upload_keeps_scans_pending_and_records_the_attempt()
    {
        var outbox = new OutboxService(store.Repository, uploader);
        var scan = await outbox.RecordScanAsync("APX-10000001", ScanType.Pickup, "courier-007", "Depot");
        uploader.IsOnline = false;

        var result = await outbox.SyncPendingAsync();

        Assert.True(result.StoppedEarly);
        var stored = (await store.Repository.GetByIdAsync(scan.Id))!;
        Assert.Equal(SyncStatus.Pending, stored.SyncStatus);
        Assert.Equal(1, stored.SyncAttempts);

        uploader.IsOnline = true;
        Assert.Equal(1, (await outbox.SyncPendingAsync()).Synced);
    }

    [Fact]
    public async Task Duplicates_reported_by_the_server_count_as_synced()
    {
        var outbox = new OutboxService(store.Repository, uploader);
        var scan = await outbox.RecordScanAsync("APX-10000001", ScanType.Pickup, "courier-007", "Depot");
        await uploader.UploadAsync([scan]);

        var result = await outbox.SyncPendingAsync();

        Assert.Equal(1, result.Synced);
        Assert.Equal(SyncStatus.Synced, (await store.Repository.GetByIdAsync(scan.Id))!.SyncStatus);
    }

    [Fact]
    public async Task Pending_scans_are_uploaded_in_batches()
    {
        var outbox = new OutboxService(store.Repository, uploader);
        for (var i = 0; i < 45; i++)
        {
            await outbox.RecordScanAsync($"APX-{20000000 + i}", ScanType.Pickup, "courier-007", "Depot");
        }

        var result = await outbox.SyncPendingAsync(batchSize: 20);

        Assert.Equal(45, result.Synced);
        Assert.Equal(3, uploader.UploadCalls);
    }

    [Fact]
    public async Task A_server_that_returns_nothing_does_not_loop_forever()
    {
        var outbox = new OutboxService(store.Repository, new SilentUploader());
        await outbox.RecordScanAsync("APX-10000001", ScanType.Pickup, "courier-007", "Depot");

        var result = await outbox.SyncPendingAsync();

        Assert.True(result.StoppedEarly);
        Assert.Equal(1, await store.Repository.CountPendingAsync());
    }

    private sealed class SilentUploader : IShipmentUploader
    {
        public Task<IReadOnlyList<ScanUploadOutcome>> UploadAsync(IReadOnlyList<ShipmentScan> scans, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanUploadOutcome>>([]);
    }
}
