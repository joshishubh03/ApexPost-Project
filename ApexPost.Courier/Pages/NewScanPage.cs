using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Monitoring;
using ApexPost.Courier.Offline.Outbox;

namespace ApexPost.Courier.Pages;

/// <summary>New Scan (D11): always saved locally first as Pending; uploaded by the outbox when online.</summary>
public sealed class NewScanPage : ContentPage
{
    private readonly OutboxService outbox;
    private readonly SyncCoordinator sync;
    private readonly ITokenStore tokens;
    private readonly PendingScanMonitor pending;
    private readonly Entry trackingNumber = new() { Placeholder = "APX-12345678", Keyboard = Keyboard.Text };
    private readonly Picker scanType = new() { Title = "Scan type", ItemsSource = Enum.GetNames<ScanType>() };
    private readonly Entry location = new() { Placeholder = "Location" };
    private readonly Entry notes = new() { Placeholder = "Notes (optional)" };
    private readonly Entry signature = new() { Placeholder = "Signature name (required for Delivered)" };
    private readonly Label result = new();
    private readonly Label pendingCount = new() { FontAttributes = FontAttributes.Bold };

    public NewScanPage(OutboxService outbox, SyncCoordinator sync, ITokenStore tokens, PendingScanMonitor pending,
        ApiCircuitMonitor circuit, IConnectivityMonitor connectivity)
    {
        this.outbox = outbox;
        this.sync = sync;
        this.tokens = tokens;
        this.pending = pending;
        Title = "New Scan";
        scanType.SelectedIndex = 0;

        var save = new Button { Text = "Save scan" };
        save.Clicked += async (_, _) => await SaveAsync();
        pending.PendingCountChanged += (_, change) =>
            MainThread.BeginInvokeOnMainThread(() => pendingCount.Text = $"Pending scans: {change.PendingCount}");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 10,
                Children =
                {
                    new OfflineBanner(circuit, connectivity),
                    pendingCount,
                    trackingNumber,
                    scanType,
                    location,
                    notes,
                    signature,
                    save,
                    result
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        pendingCount.Text = $"Pending scans: {pending.PendingCount}";
    }

    private async Task SaveAsync()
    {
        var courierId = JwtClaimsReader.TryRead(await tokens.GetAccessTokenAsync())?.CourierId;
        if (courierId is null)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        try
        {
            var type = Enum.Parse<ScanType>((string)scanType.SelectedItem);
            if (type == ScanType.Delivered && string.IsNullOrWhiteSpace(signature.Text))
            {
                result.Text = "A Delivered scan needs the recipient's signature name.";
                return;
            }

            var scan = await outbox.RecordScanAsync(trackingNumber.Text, type, courierId, location.Text, notes.Text, signature.Text);
            result.Text = $"Saved on this device: {scan.TrackingNumber} {scan.ScanType} (Pending)";
            trackingNumber.Text = location.Text = notes.Text = signature.Text = string.Empty;

            // Try to upload straight away; if offline or the circuit is open the scan simply stays Pending.
            _ = sync.SyncNowAsync();
        }
        catch (ArgumentException exception)
        {
            result.Text = exception.Message;
        }
    }
}
