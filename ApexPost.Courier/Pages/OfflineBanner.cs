using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Core.Sync;

namespace ApexPost.Courier.Pages;

/// <summary>
/// Red banner shown while the circuit breaker is open ("Server unavailable – working offline") or the device has
/// no internet. Scans are still saved locally either way.
/// </summary>
public sealed class OfflineBanner : ContentView
{
    private readonly ApiCircuitMonitor circuit;
    private readonly IConnectivityMonitor connectivity;
    private readonly Label text = new() { TextColor = Colors.White, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };

    public OfflineBanner(ApiCircuitMonitor circuit, IConnectivityMonitor connectivity)
    {
        this.circuit = circuit;
        this.connectivity = connectivity;
        Content = new Border { BackgroundColor = Color.FromArgb("#B00020"), Padding = new Thickness(12, 8), StrokeThickness = 0, Content = text };
        circuit.StateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        connectivity.ConnectivityChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        if (circuit.IsServerUnavailable)
        {
            text.Text = CourierResilience.OfflineBanner;
            IsVisible = true;
        }
        else if (!connectivity.IsOnline)
        {
            text.Text = "No network – scans are saved on this device";
            IsVisible = true;
        }
        else
        {
            IsVisible = false;
        }
    }
}
