using System.Net;
using System.Text.Json;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Core.Tests;

public sealed class OfflineSyncTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "apexpost-core-tests", Guid.NewGuid().ToString("N"));
    private readonly CourierDatabase database;
    private readonly CouchbaseScanRepository repository;

    public OfflineSyncTests()
    {
        database = new CourierDatabase(directory);
        repository = new CouchbaseScanRepository(database);
    }

    public void Dispose()
    {
        database.Dispose();
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Uploader_maps_server_results_to_synced_and_failed()
    {
        var keep = ShipmentScan.CreatePending("APX-10000001", ScanType.Pickup, "courier-007", "Depot", DateTimeOffset.UtcNow.AddMinutes(-5));
        var reject = ShipmentScan.CreatePending("APX-10000001", ScanType.Delivered, "courier-007", "Door", DateTimeOffset.UtcNow.AddMinutes(-1), signatureName: "J. Doe");
        var handler = new StubHandler((_, _) => StubHandler.Json(HttpStatusCode.OK,
            $$"""{"accepted":1,"duplicatesSkipped":0,"rejected":[{"scanId":"{{reject.Id}}","reason":"Invalid status transition PickedUp -> Delivered"}]}"""));
        using var api = new TestApi(handler);
        var uploader = new ApiShipmentUploader(() => api.Client, NullLogger<ApiShipmentUploader>.Instance);

        var outcomes = await uploader.UploadAsync([keep, reject]);

        Assert.Equal(ScanUploadResult.Accepted, outcomes.Single(outcome => outcome.ScanId == keep.Id).Result);
        var rejected = outcomes.Single(outcome => outcome.ScanId == reject.Id);
        Assert.Equal(ScanUploadResult.Rejected, rejected.Result);
        Assert.Contains("transition", rejected.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Server_down_keeps_scans_pending()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var api = new TestApi(handler);
        var outbox = new OutboxService(repository, new ApiShipmentUploader(() => api.Client, NullLogger<ApiShipmentUploader>.Instance));
        await outbox.RecordScanAsync("APX-10000001", ScanType.Pickup, "courier-007", "Depot");

        var result = await outbox.SyncPendingAsync();

        Assert.True(result.StoppedEarly);
        Assert.Equal(1, await repository.CountPendingAsync());
    }

    [Fact]
    public async Task Reconnecting_uploads_pending_scans_in_batches_of_20_and_marks_them_synced()
    {
        var uploadsPerCall = new List<int>();
        var handler = new StubHandler((request, _) =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var count = JsonDocument.Parse(body).RootElement.GetProperty("scans").GetArrayLength();
            lock (uploadsPerCall)
            {
                uploadsPerCall.Add(count);
            }

            return StubHandler.Json(HttpStatusCode.OK, $$"""{"accepted":{{count}},"duplicatesSkipped":0,"rejected":[]}""");
        });
        using var api = new TestApi(handler);
        var outbox = new OutboxService(repository, new ApiShipmentUploader(() => api.Client, NullLogger<ApiShipmentUploader>.Instance));
        for (var i = 0; i < 45; i++)
        {
            await outbox.RecordScanAsync("APX-10000001", ScanType.Pickup, "courier-007", $"Depot {i}",
                scannedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-60 + i));
        }

        var connectivity = new FakeConnectivity { IsOnline = false };
        using var coordinator = new SyncCoordinator(outbox, connectivity, TimeProvider.System, NullLogger<SyncCoordinator>.Instance);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.StatusChanged += (_, _) =>
        {
            if (coordinator.LastResult is { Synced: > 0 })
            {
                finished.TrySetResult();
            }
        };
        coordinator.Start();
        Assert.Equal(0, handler.Calls);

        connectivity.GoOnline();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await repository.CountPendingAsync());
        Assert.Equal(new[] { 20, 20, 5 }, uploadsPerCall);
        Assert.Equal(45, coordinator.TotalSynced);
        Assert.NotNull(coordinator.LastSuccessfulSyncUtc);
    }

    private sealed class FakeConnectivity : IConnectivityMonitor
    {
        public bool IsOnline { get; set; }

        public event EventHandler<bool>? ConnectivityChanged;

        public void GoOnline()
        {
            IsOnline = true;
            ConnectivityChanged?.Invoke(this, true);
        }
    }
}
