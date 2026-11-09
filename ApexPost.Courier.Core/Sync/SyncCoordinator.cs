using ApexPost.Courier.Offline.Outbox;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Core.Sync;

/// <summary>Network reachability as seen by the device (MAUI: Connectivity.ConnectivityChanged).</summary>
public interface IConnectivityMonitor
{
    bool IsOnline { get; }

    event EventHandler<bool>? ConnectivityChanged;
}

/// <summary>
/// Uploads pending scans automatically: when connectivity returns, and on demand. Runs the Part A outbox with
/// batches of 20 through the real API uploader and keeps the numbers the Sync Status screen shows.
/// </summary>
public sealed class SyncCoordinator : IDisposable
{
    public const int BatchSize = 20;

    private readonly OutboxService outbox;
    private readonly IConnectivityMonitor connectivity;
    private readonly TimeProvider clock;
    private readonly ILogger<SyncCoordinator> logger;
    private bool started;

    public SyncCoordinator(OutboxService outbox, IConnectivityMonitor connectivity, TimeProvider clock, ILogger<SyncCoordinator> logger)
    {
        this.outbox = outbox;
        this.connectivity = connectivity;
        this.clock = clock;
        this.logger = logger;
    }

    public event EventHandler? StatusChanged;

    public DateTimeOffset? LastSuccessfulSyncUtc { get; private set; }
    public DateTimeOffset? LastAttemptUtc { get; private set; }
    public OutboxRunResult? LastResult { get; private set; }
    public int TotalSynced { get; private set; }
    public int TotalFailed { get; private set; }
    public string? LastError { get; private set; }
    public bool IsOnline => connectivity.IsOnline;

    /// <summary>Subscribes to connectivity changes and syncs immediately if the device is online.</summary>
    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        connectivity.ConnectivityChanged += OnConnectivityChanged;
        if (connectivity.IsOnline)
        {
            _ = SyncNowAsync();
        }
    }

    public async Task<OutboxRunResult> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (!connectivity.IsOnline)
        {
            LastError = "Offline - scans are kept on the device.";
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return new OutboxRunResult(0, 0, 0, StoppedEarly: true, LastError);
        }

        LastAttemptUtc = clock.GetUtcNow();
        OutboxRunResult result;
        try
        {
            result = await outbox.SyncPendingAsync(BatchSize, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A sync failure must never crash the app; the scans stay Pending.
            logger.LogError(exception, "Sync run failed");
            result = new OutboxRunResult(0, 0, 0, StoppedEarly: true, "Sync failed - will retry.");
        }

        LastResult = result;
        TotalSynced += result.Synced;
        TotalFailed += result.Failed;
        LastError = result.Error;
        if (!result.StoppedEarly)
        {
            LastSuccessfulSyncUtc = LastAttemptUtc;
        }

        logger.LogInformation("Sync run: attempted {Attempted}, synced {Synced}, failed {Failed}, stopped early {Stopped}",
            result.Attempted, result.Synced, result.Failed, result.StoppedEarly);
        StatusChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void Dispose()
    {
        if (started)
        {
            connectivity.ConnectivityChanged -= OnConnectivityChanged;
        }
    }

    private void OnConnectivityChanged(object? sender, bool online)
    {
        logger.LogInformation("Connectivity changed: {State}", online ? "online" : "offline");
        StatusChanged?.Invoke(this, EventArgs.Empty);
        if (online)
        {
            _ = SyncNowAsync();
        }
    }
}
