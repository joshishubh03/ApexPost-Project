using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApexPost.Courier.Core;

public static class CourierCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the typed <see cref="ApexApiClient"/> through IHttpClientFactory with, outermost first, the
    /// <see cref="AuthHeaderHandler"/> (bearer token from the token store) and the Polly resilience pipeline.
    /// The caller registers an <see cref="ITokenStore"/> implementation.
    /// </summary>
    public static IHttpClientBuilder AddApexApiClient(
        this IServiceCollection services,
        Uri baseAddress,
        CourierResilienceOptions? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        services.TryAddSingleton<SessionEvents>();
        services.TryAddSingleton<ApiCircuitMonitor>();
        services.TryAddTransient<AuthHeaderHandler>();
        services.TryAddTransient<Func<ApexApiClient>>(provider => () => provider.GetRequiredService<ApexApiClient>());
        services.TryAddSingleton<IShipmentUploader, ApiShipmentUploader>();

        return services
            .AddHttpClient<ApexApiClient>(client =>
            {
                client.BaseAddress = baseAddress;
                // Timeouts are enforced by the resilience pipeline (per attempt + overall); this is only a backstop.
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .AddCourierResilience(resilience);
    }
}
