using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Courier.Offline.Outbox;

/// <summary>
/// Outbox pattern for the courier device. New scans are saved locally first (<see cref="RecordScanAsync"/>) with
/// syncStatus = Pending; <see cref="SyncPendingAsync"/> later reads the pending scans in batches, uploads them and
/// marks each one Synced (accepted or already on the server) or Failed (rejected by the server). A transient error
/// (offline, timeout, server down) leaves the whole batch Pending so nothing is lost and the next run retries.
/// </summary>
public sealed class OutboxService(
    IScanRepository repository,
    IShipmentUploader uploader,
    TimeProvider? timeProvider = null,
    ILogger<OutboxService>? logger = null)
{
    public const int DefaultBatchSize = 20;
    private const int MaximumBatchesPerRun = 50;

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly ILogger log = logger ?? NullLogger<OutboxService>.Instance;
    private readonly SemaphoreSlim runGate = new(1, 1);

    /// <summary>Saves a new scan on the device. It is Pending until the outbox uploads it.</summary>
    public async Task<ShipmentScan> RecordScanAsync(
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        string? notes = null,
        string? signatureName = null,
        DateTimeOffset? scannedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        var scan = ShipmentScan.CreatePending(
            trackingNumber, scanType, courierId, location, scannedAtUtc ?? clock.GetUtcNow(), notes, signatureName);
        await repository.CreateAsync(scan, cancellationToken);
        log.LogInformation("Saved scan {ScanId} for {TrackingNumber} locally as Pending", scan.Id, scan.TrackingNumber);
        return scan;
    }

    /// <summary>
    /// Uploads pending scans in batches of <paramref name="batchSize"/> until none are left, the uploader reports a
    /// transient failure, or the run is cancelled. Only one run executes at a time on a device.
    /// </summary>
    public async Task<OutboxRunResult> SyncPendingAsync(int batchSize = DefaultBatchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        if (!await runGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            log.LogInformation("Outbox run skipped: another run is already in progress");
            return OutboxRunResult.Skipped;
        }

        try
        {
            var attempted = 0;
            var synced = 0;
            var failed = 0;

            for (var batchNumber = 1; batchNumber <= MaximumBatchesPerRun; batchNumber++)
            {
                var batch = await repository.ListPendingAsync(batchSize, cancellationToken);
                if (batch.Count == 0)
                {
                    break;
                }

                attempted += batch.Count;
                IReadOnlyList<ScanUploadOutcome> outcomes;
                try
                {
                    outcomes = await uploader.UploadAsync(batch, cancellationToken);
                }
                catch (ShipmentUploadUnavailableException exception)
                {
                    await MarkTransientAsync(batch, exception.Message, cancellationToken);
                    log.LogWarning("Outbox run stopped: server unavailable ({Reason}); {Count} scan(s) stay Pending",
                        exception.Message, batch.Count);
                    return new OutboxRunResult(attempted, synced, failed, StoppedEarly: true, exception.Message);
                }

                var byId = outcomes.ToDictionary(outcome => outcome.ScanId, StringComparer.Ordinal);
                var changed = 0;
                foreach (var scan in batch)
                {
                    var now = clock.GetUtcNow();
                    if (!byId.TryGetValue(scan.Id, out var outcome))
                    {
                        // The server did not answer for this scan: keep it Pending (retried next run).
                        await repository.UpdateAsync(scan.MarkAttemptFailedTransiently(now, "No result returned for this scan."), cancellationToken);
                        continue;
                    }

                    if (outcome.Result is ScanUploadResult.Accepted or ScanUploadResult.Duplicate)
                    {
                        await repository.UpdateAsync(scan.MarkSynced(now), cancellationToken);
                        synced++;
                    }
                    else
                    {
                        await repository.UpdateAsync(scan.MarkFailed(now, outcome.Reason ?? "Rejected by the server."), cancellationToken);
                        failed++;
                        log.LogWarning("Scan {ScanId} rejected: {Reason}", scan.Id, outcome.Reason);
                    }

                    changed++;
                }

                log.LogInformation("Outbox batch {Batch}: {Count} uploaded, {Changed} resolved", batchNumber, batch.Count, changed);
                if (changed == 0)
                {
                    // Nothing moved out of Pending: stop instead of re-sending the same batch forever.
                    return new OutboxRunResult(attempted, synced, failed, StoppedEarly: true, "The server returned no results.");
                }
            }

            return new OutboxRunResult(attempted, synced, failed, StoppedEarly: false, Error: null);
        }
        finally
        {
            runGate.Release();
        }
    }

    /// <summary>Puts Failed scans back into the outbox (for example after the shipment was corrected on the server).</summary>
    public async Task<int> RequeueFailedAsync(CancellationToken cancellationToken = default)
    {
        var requeued = 0;
        foreach (var scan in await repository.ListAllAsync(cancellationToken))
        {
            if (scan.SyncStatus == SyncStatus.Failed)
            {
                await repository.UpdateAsync(scan with { SyncStatus = SyncStatus.Pending }, cancellationToken);
                requeued++;
            }
        }

        return requeued;
    }

    private async Task MarkTransientAsync(IReadOnlyList<ShipmentScan> batch, string reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        foreach (var scan in batch)
        {
            await repository.UpdateAsync(scan.MarkAttemptFailedTransiently(now, reason), cancellationToken);
        }
    }
}

/// <summary>Summary of one outbox run.</summary>
public sealed record OutboxRunResult(int Attempted, int Synced, int Failed, bool StoppedEarly, string? Error)
{
    public static OutboxRunResult Skipped { get; } = new(0, 0, 0, StoppedEarly: true, "Another sync run is in progress.");
}
