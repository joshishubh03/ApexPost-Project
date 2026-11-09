using System.Net;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Resilience;

namespace ApexPost.Courier.Core.Tests;

public sealed class ResilienceTests
{
    [Fact]
    public async Task Transient_5xx_is_retried_and_then_succeeds()
    {
        var handler = new StubHandler((_, call) => call < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : StubHandler.Json(HttpStatusCode.OK, TestApi.ShipmentJson));
        using var api = new TestApi(handler);

        var shipment = await api.Client.GetShipmentAsync("APX-10000001");

        Assert.Equal(ShipmentStatusDto.PickedUp, shipment.Status);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Network_errors_and_408_are_retried()
    {
        var handler = new StubHandler((_, call) => call switch
        {
            1 => throw new HttpRequestException("connection reset"),
            2 => new HttpResponseMessage(HttpStatusCode.RequestTimeout),
            _ => StubHandler.Json(HttpStatusCode.OK, TestApi.ShipmentJson)
        });
        using var api = new TestApi(handler);

        await api.Client.GetShipmentAsync("APX-10000001");

        Assert.Equal(3, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Client_errors_are_never_retried(HttpStatusCode status)
    {
        var handler = new StubHandler((_, _) => StubHandler.Json(status, """{"title":"nope","status":0}""", "application/problem+json"));
        using var api = new TestApi(handler);

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Non_idempotent_requests_are_not_retried()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var api = new TestApi(handler);
        var http = api.Factory.CreateClient(nameof(ApexApiClient));

        using var response = await http.PostAsync("api/v2/something", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Scan_upload_post_is_retried_because_it_is_idempotent()
    {
        var handler = new StubHandler((_, call) => call == 1
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : StubHandler.Json(HttpStatusCode.OK, """{"accepted":1,"duplicatesSkipped":0,"rejected":[]}"""));
        using var api = new TestApi(handler);

        var result = await api.Client.UploadScansAsync("APX-10000001", new ScanUploadRequestDto());

        Assert.Equal(1, result.Accepted);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Persistent_failures_end_as_ApiUnavailableException_after_three_retries()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var api = new TestApi(handler);

        await Assert.ThrowsAsync<ApiUnavailableException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task Circuit_opens_after_repeated_failures_then_fails_fast_and_reports_the_banner_state()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var options = new CourierResilienceOptions
        {
            MaxRetryAttempts = 3,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            CircuitFailureRatio = 0.5,
            CircuitMinimumThroughput = 3,
            CircuitSamplingDuration = TimeSpan.FromSeconds(30),
            CircuitBreakDuration = TimeSpan.FromSeconds(30)
        };
        using var api = new TestApi(handler, options);

        var first = await Assert.ThrowsAsync<ApiUnavailableException>(() => api.Client.GetShipmentAsync("APX-10000001"));
        var callsWhenOpened = handler.Calls;
        var second = await Assert.ThrowsAsync<ApiUnavailableException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        Assert.Equal(ApiCircuitState.Open, api.Circuit.State);
        Assert.True(api.Circuit.IsServerUnavailable);
        Assert.Equal(callsWhenOpened, handler.Calls);
        Assert.Equal(CourierResilience.OfflineBanner, second.UserMessage);
        Assert.NotNull(first);
    }

    [Fact]
    public void Transient_classification_matches_the_policy()
    {
        Assert.True(CourierResilience.IsTransient(HttpStatusCode.RequestTimeout));
        Assert.True(CourierResilience.IsTransient(HttpStatusCode.ServiceUnavailable));
        Assert.False(CourierResilience.IsTransient(HttpStatusCode.TooManyRequests));
        Assert.False(CourierResilience.IsTransient(HttpStatusCode.Unauthorized));
        Assert.True(CourierResilience.IsIdempotent(new HttpRequestMessage(HttpMethod.Get, "x")));
        Assert.False(CourierResilience.IsIdempotent(new HttpRequestMessage(HttpMethod.Post, "x")));
        var flagged = new HttpRequestMessage(HttpMethod.Post, "x");
        flagged.Options.Set(CourierResilience.IdempotentRequest, true);
        Assert.True(CourierResilience.IsIdempotent(flagged));
    }
}
