using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Outbox;

/// <summary>
/// Part A stand-in for the server (replaced by the real API client in Part D). Behaviour is deterministic so the
/// harness and the tests can show every path:
/// <list type="bullet">
/// <item>notes containing <c>#reject</c> - the "server" rejects the scan (scan becomes Failed);</item>
/// <item>a scan ID it has already received - reported as a duplicate (still Synced: uploads are idempotent);</item>
/// <item><see cref="IsOnline"/> = false - throws <see cref="ShipmentUploadUnavailableException"/> (scans stay Pending);</item>
/// <item>everything else is accepted.</item>
/// </list>
/// </summary>
public sealed class FakeShipmentUploader : IShipmentUploader
{
    public const string RejectMarker = "#reject";

    private readonly HashSet<string> received = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Simulates network coverage. Toggle it in the harness to see scans stay Pending.</summary>
    public bool IsOnline { get; set; } = true;

    /// <summary>Optional artificial latency per upload call.</summary>
    public TimeSpan Latency { get; set; } = TimeSpan.Zero;

    public int UploadCalls { get; private set; }

    public async Task<IReadOnlyList<ScanUploadOutcome>> UploadAsync(
        IReadOnlyList<ShipmentScan> scans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scans);
        if (Latency > TimeSpan.Zero)
        {
            await Task.Delay(Latency, cancellationToken);
        }

        lock (gate)
        {
            UploadCalls++;
            if (!IsOnline)
            {
                throw new ShipmentUploadUnavailableException("No network coverage (simulated).");
            }

            var outcomes = new List<ScanUploadOutcome>(scans.Count);
            foreach (var scan in scans)
            {
                if (scan.Notes?.Contains(RejectMarker, StringComparison.OrdinalIgnoreCase) == true)
                {
                    outcomes.Add(ScanUploadOutcome.Rejected(scan.Id, "Rejected by the fake server (#reject in notes)."));
                }
                else if (!received.Add(scan.Id))
                {
                    outcomes.Add(ScanUploadOutcome.Duplicate(scan.Id));
                }
                else
                {
                    outcomes.Add(ScanUploadOutcome.Accepted(scan.Id));
                }
            }

            return outcomes;
        }
    }
}
