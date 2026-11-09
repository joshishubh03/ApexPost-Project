using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Monitoring;

namespace ApexPost.Courier.Pages;

/// <summary>Pending Scans (D11): live count from the Part A change listener plus the list of unsent scans.</summary>
public sealed class PendingScansPage : ContentPage
{
    private readonly IScanRepository repository;
    private readonly SyncCoordinator sync;
    private readonly Label count = new() { FontSize = 20, FontAttributes = FontAttributes.Bold };
    private readonly CollectionView list = new();

    public PendingScansPage(IScanRepository repository, PendingScanMonitor pending, SyncCoordinator sync,
        ApiCircuitMonitor circuit, IConnectivityMonitor connectivity)
    {
        this.repository = repository;
        this.sync = sync;
        Title = "Pending";

        list.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label { Padding = new Thickness(0, 6) };
            label.SetBinding(Label.TextProperty, nameof(ScanRow.Text));
            return label;
        });

        // The change listener fires on Couchbase's thread; marshal to the UI thread.
        pending.PendingCountChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());

        var syncNow = new Button { Text = "Sync now" };
        syncNow.Clicked += async (_, _) =>
        {
            await sync.SyncNowAsync();
            await RefreshAsync();
        };

        var grid = new Grid
        {
            Padding = 20,
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        grid.Add(new OfflineBanner(circuit, connectivity), 0, 0);
        grid.Add(count, 0, 1);
        grid.Add(syncNow, 0, 2);
        grid.Add(list, 0, 3);
        Content = grid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var scans = await repository.ListPendingAsync();
        count.Text = $"Pending scans: {scans.Count}";
        list.ItemsSource = scans.Select(scan => new ScanRow(scan)).ToList();
    }

    private sealed record ScanRow(ShipmentScan Scan)
    {
        public string Text =>
            $"{Scan.TrackingNumber}  {Scan.ScanType}  {Scan.Location}  {Scan.ScannedAtUtc.ToLocalTime():HH:mm}" +
            (Scan.SyncAttempts > 0 ? $"  (attempts {Scan.SyncAttempts}: {Scan.LastSyncError})" : string.Empty);
    }
}
