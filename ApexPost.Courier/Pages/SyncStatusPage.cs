using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Outbox;

namespace ApexPost.Courier.Pages;

/// <summary>Sync Status (D11): connectivity, last sync time, circuit state and failures.</summary>
public sealed class SyncStatusPage : ContentPage
{
    private readonly SyncCoordinator sync;
    private readonly ApiCircuitMonitor circuit;
    private readonly IScanRepository repository;
    private readonly Label details = new() { FontFamily = "Courier New" };
    private readonly Label failures = new();

    public SyncStatusPage(SyncCoordinator sync, ApiCircuitMonitor circuit, IConnectivityMonitor connectivity,
        IScanRepository repository, OutboxService outbox)
    {
        this.sync = sync;
        this.circuit = circuit;
        this.repository = repository;
        Title = "Sync";

        sync.StatusChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());
        circuit.StateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());

        var syncNow = new Button { Text = "Sync now" };
        syncNow.Clicked += async (_, _) => await sync.SyncNowAsync();
        var retryFailed = new Button { Text = "Retry failed scans" };
        retryFailed.Clicked += async (_, _) =>
        {
            await outbox.RequeueFailedAsync();
            await sync.SyncNowAsync();
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children = { new OfflineBanner(circuit, connectivity), details, syncNow, retryFailed, new Label { Text = "Failed scans", FontAttributes = FontAttributes.Bold }, failures }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var last = sync.LastResult;
        details.Text = string.Join(Environment.NewLine,
            $"Network:        {(sync.IsOnline ? "online" : "OFFLINE")}",
            $"Circuit:        {circuit.State}{(circuit.LastChangedUtc is { } changed ? $" since {changed.ToLocalTime():HH:mm:ss}" : string.Empty)}",
            $"Last sync:      {(sync.LastSuccessfulSyncUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "never")}",
            $"Last attempt:   {(sync.LastAttemptUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "-")}",
            $"Last run:       {(last is null ? "-" : $"{last.Synced} synced, {last.Failed} failed of {last.Attempted}")}",
            $"Totals:         {sync.TotalSynced} synced, {sync.TotalFailed} failed",
            $"Last error:     {sync.LastError ?? "-"}");

        var failed = (await repository.ListAllAsync()).Where(scan => scan.SyncStatus == SyncStatus.Failed).ToList();
        failures.Text = failed.Count == 0
            ? "None"
            : string.Join(Environment.NewLine, failed.Select(scan => $"{scan.TrackingNumber} {scan.ScanType}: {scan.LastSyncError}"));
    }
}
