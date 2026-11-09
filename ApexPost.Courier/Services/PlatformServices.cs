using System.Net.Security;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Sync;

namespace ApexPost.Courier.Services;

/// <summary>
/// The access token lives only in MAUI SecureStorage (Android Keystore / Windows DPAPI) - never in Preferences,
/// files or logs.
/// </summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string AccessTokenKey = "apexpost.access_token";

    public Task<string?> GetAccessTokenAsync() => SecureStorage.Default.GetAsync(AccessTokenKey);

    public Task SaveAccessTokenAsync(string accessToken) => SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        return Task.CompletedTask;
    }
}

/// <summary>MAUI Connectivity adapter: "online" means internet access (airplane mode -> offline).</summary>
public sealed class MauiConnectivityMonitor : IConnectivityMonitor, IDisposable
{
    public MauiConnectivityMonitor() => Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

    public event EventHandler<bool>? ConnectivityChanged;

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public void Dispose() => Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs change) =>
        ConnectivityChanged?.Invoke(this, change.NetworkAccess == NetworkAccess.Internet);
}

/// <summary>Base URLs per platform and resilience values, bound from the embedded appsettings.json.</summary>
public sealed class ApiSettings
{
    public string AndroidEmulatorBaseUrl { get; init; } = "https://10.0.2.2:7301/";
    public string WindowsBaseUrl { get; init; } = "https://localhost:7301/";
    public Core.Resilience.CourierResilienceOptions Resilience { get; init; } = new();

    public Uri BaseAddressForThisDevice() =>
        new(DeviceInfo.Platform == DevicePlatform.Android ? AndroidEmulatorBaseUrl : WindowsBaseUrl);
}

#if DEBUG
/// <summary>
/// DEBUG builds only: accept the ASP.NET Core development certificate for the local API (localhost on Windows,
/// 10.0.2.2 = host loopback from the Android emulator), which the emulator does not trust. Any other host, and
/// every Release build, uses normal certificate validation. See SECURITY.md.
/// </summary>
internal static class DevelopmentCertificates
{
    public static HttpMessageHandler CreateHandler() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
            errors == SslPolicyErrors.None ||
            (request.RequestUri is { } uri && (uri.IsLoopback || uri.Host == "10.0.2.2"))
    };
}
#endif
