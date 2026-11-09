using ApexPost.Courier.Core;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Monitoring;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Storage;
using ApexPost.Courier.Pages;
using ApexPost.Courier.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        using (var settings = typeof(MauiProgram).Assembly.GetManifestResourceStream("ApexPost.Courier.appsettings.json"))
        {
            if (settings is not null)
            {
                builder.Configuration.AddJsonStream(settings);
            }
        }

        var api = builder.Configuration.GetSection("Api").Get<ApiSettings>() ?? new ApiSettings();

        // Platform adapters.
        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddSingleton<IConnectivityMonitor, MauiConnectivityMonitor>();
        builder.Services.AddSingleton(TimeProvider.System);

        // Part A offline store: ONE CourierDatabase per process (see OFFLINE-DESIGN.md).
        builder.Services.AddSingleton(provider => new CourierDatabase(
            Path.Combine(FileSystem.AppDataDirectory, "courier"),
            CourierDatabase.DefaultName,
            provider.GetRequiredService<ILogger<CourierDatabase>>()));
        builder.Services.AddSingleton<IScanRepository>(provider => new CouchbaseScanRepository(provider.GetRequiredService<CourierDatabase>()));
        builder.Services.AddSingleton(provider => new PendingScanMonitor(
            provider.GetRequiredService<CourierDatabase>(),
            provider.GetRequiredService<IScanRepository>(),
            provider.GetRequiredService<ILogger<PendingScanMonitor>>()));

        // Typed API client via IHttpClientFactory + AuthHeaderHandler + Polly (retry, circuit breaker, timeouts).
        // AddApexApiClient also registers ApiShipmentUploader as the IShipmentUploader (replacing Part A's fake).
        var http = builder.Services.AddApexApiClient(api.BaseAddressForThisDevice(), api.Resilience);
#if DEBUG
        http.ConfigurePrimaryHttpMessageHandler(DevelopmentCertificates.CreateHandler);
#endif
        builder.Services.AddSingleton(provider => new OutboxService(
            provider.GetRequiredService<IScanRepository>(),
            provider.GetRequiredService<IShipmentUploader>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<OutboxService>>()));
        builder.Services.AddSingleton<SyncCoordinator>();

        // UI.
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<NewScanPage>();
        builder.Services.AddTransient<PendingScansPage>();
        builder.Services.AddTransient<SyncStatusPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
