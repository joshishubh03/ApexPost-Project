using System.Net;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Resilience;

namespace ApexPost.Courier.Core.Tests;

/// <summary>
/// Week 5: the typed <see cref="ApexApiClient"/> (Week 4 Part D) against a fake <see cref="HttpMessageHandler"/>.
/// The client is built exactly as the app builds it (IHttpClientFactory + auth handler + Polly pipeline); only the
/// last handler is replaced, so no request leaves the process.
/// <para>
/// What this does not prove: that the real API returns these responses (the API integration tests do), or TLS.
/// </para>
/// </summary>
public sealed class ApexApiClientHttpTests
{
    private const string ProblemJson = """
        {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.10","title":"Conflict","status":409,
         "detail":"Shipment APX-10000001 cannot move from Delivered to InTransit.","traceId":"00-abc-01"}
        """;

    [Fact]
    public async Task GetShipmentAsync_200WithJson_ReturnsTheShipmentFromTheV2Route()
    {
        // Arrange
        HttpRequestMessage? sent = null;
        var handler = new StubHandler((request, _) =>
        {
            sent = request;
            return StubHandler.Json(HttpStatusCode.OK, TestApi.ShipmentJson);
        });
        using var api = new TestApi(handler);

        // Act
        var shipment = await api.Client.GetShipmentAsync("APX-10000001");

        // Assert
        Assert.Equal("APX-10000001", shipment.TrackingNumber);
        Assert.Equal(ShipmentStatusDto.PickedUp, shipment.Status);
        Assert.Equal("Maplewood", shipment.Recipient?.City);
        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("/api/v2/shipments/APX-10000001", sent?.RequestUri?.AbsolutePath);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetShipmentAsync_404_ThrowsApiProblemExceptionAndDoesNotRetry()
    {
        // Arrange
        var handler = new StubHandler((_, _) => StubHandler.Json(HttpStatusCode.NotFound,
            """{"title":"Not Found","status":404,"detail":"No shipment with tracking number APX-10000009 exists."}""",
            "application/problem+json"));
        using var api = new TestApi(handler);

        // Act
        var exception = await Assert.ThrowsAsync<ApiProblemException>(() => api.Client.GetShipmentAsync("APX-10000009"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal("That tracking number is not known to the server.", exception.UserMessage);
        Assert.Equal(1, handler.Calls);   // a 4xx is never retried
    }

    [Fact]
    public async Task GetShipmentAsync_500OnEveryAttempt_RetriesThreeTimesThenThrowsApiUnavailable()
    {
        // Arrange
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var api = new TestApi(handler);

        // Act
        var exception = await Assert.ThrowsAsync<ApiUnavailableException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        // Assert
        Assert.Equal(4, handler.Calls);   // 1 attempt + 3 retries
        Assert.Contains("offline", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetShipmentAsync_ProblemDetailsBody_ExposesTheProblemAndUsesItsDetailAsTheMessage()
    {
        // Arrange
        var handler = new StubHandler((_, _) => StubHandler.Json(HttpStatusCode.Conflict, ProblemJson, "application/problem+json"));
        using var api = new TestApi(handler);

        // Act
        var exception = await Assert.ThrowsAsync<ApiProblemException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("Conflict", exception.Problem?.Title);
        Assert.Equal(409, exception.Problem?.Status);
        Assert.Equal("Shipment APX-10000001 cannot move from Delivered to InTransit.", exception.UserMessage);
    }

    [Fact]
    public async Task GetShipmentAsync_ServerNeverAnswers_TimesOutPerAttemptAndThrowsApiUnavailable()
    {
        // Arrange: the fake server waits until the client gives up (cancellation), so the per-attempt timeout fires.
        var handler = new HangingHandler();
        var options = new CourierResilienceOptions
        {
            MaxRetryAttempts = 1,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            AttemptTimeout = TimeSpan.FromMilliseconds(50),
            TotalRequestTimeout = TimeSpan.FromSeconds(10),
            CircuitFailureRatio = 0.5,
            CircuitMinimumThroughput = 50,
            CircuitSamplingDuration = TimeSpan.FromSeconds(30),
            CircuitBreakDuration = TimeSpan.FromSeconds(30)
        };
        using var api = new TestApi(handler, options);

        // Act
        var exception = await Assert.ThrowsAsync<ApiUnavailableException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        // Assert
        Assert.Contains("did not answer in time", exception.UserMessage, StringComparison.Ordinal);
        Assert.Equal(2, handler.Calls);   // the timeout is transient, so it was retried once
    }

    [Fact]
    public async Task GetShipmentAsync_CallerCancels_ThrowsOperationCanceledInsteadOfWorkingOffline()
    {
        // Arrange
        var handler = new HangingHandler();
        using var api = new TestApi(handler);
        using var cancellation = new CancellationTokenSource();
        var call = api.Client.GetShipmentAsync("APX-10000001", cancellation.Token);
        await handler.Started.WaitAsync(TimeSpan.FromSeconds(10));

        // Act
        await cancellation.CancelAsync();

        // Assert: the user cancelled, so this is not reported as "server unavailable".
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>A server that accepts the request and never answers until the request is cancelled.</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public Task Started => started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
