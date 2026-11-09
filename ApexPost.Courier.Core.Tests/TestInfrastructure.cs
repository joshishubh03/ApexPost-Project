using System.Net;
using System.Text;
using ApexPost.Courier.Core;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Courier.Core.Tests;

/// <summary>Fake server: answers every request with the delegate's response and counts the calls.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
{
    private int calls;

    public int Calls => Volatile.Read(ref calls);

    public List<string?> AuthorizationHeaders { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref calls);
        lock (AuthorizationHeaders)
        {
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
        }

        var response = respond(request, call);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };
}

internal sealed class TestApi : IDisposable
{
    public static readonly CourierResilienceOptions FastOptions = new()
    {
        MaxRetryAttempts = 3,
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        AttemptTimeout = TimeSpan.FromSeconds(5),
        TotalRequestTimeout = TimeSpan.FromSeconds(20),
        CircuitFailureRatio = 0.5,
        CircuitMinimumThroughput = 50,
        CircuitSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakDuration = TimeSpan.FromSeconds(30)
    };

    private readonly ServiceProvider provider;

    public TestApi(StubHandler handler, CourierResilienceOptions? options = null, string? token = "test-token")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var tokens = new InMemoryTokenStore();
        if (token is not null)
        {
            tokens.SaveAccessTokenAsync(token).GetAwaiter().GetResult();
        }

        services.AddSingleton<ITokenStore>(tokens);
        services.AddApexApiClient(new Uri("https://api.apexpost.test/"), options ?? FastOptions)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        provider = services.BuildServiceProvider();
        Tokens = tokens;
    }

    public InMemoryTokenStore Tokens { get; }
    public ApexApiClient Client => provider.GetRequiredService<ApexApiClient>();
    public ApiCircuitMonitor Circuit => provider.GetRequiredService<ApiCircuitMonitor>();
    public SessionEvents Session => provider.GetRequiredService<SessionEvents>();
    public IHttpClientFactory Factory => provider.GetRequiredService<IHttpClientFactory>();
    public T Get<T>() where T : notnull => provider.GetRequiredService<T>();

    public void Dispose() => provider.Dispose();

    public const string ShipmentJson = """
        {"trackingNumber":"APX-10000001","status":"PickedUp","weightKg":2.5,
         "recipient":{"name":"Jordan Example","phone":"+15550100123","address":"12 Elm Row","city":"Maplewood"},
         "latestScan":{"scanType":"Pickup","location":"Depot","scannedAtUtc":"2026-11-09T08:00:00Z"},
         "createdAtUtc":"2026-11-09T07:00:00Z","updatedAtUtc":"2026-11-09T08:00:00Z"}
        """;
}
