using System.Net;
using ApexPost.Application.Models;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure;

namespace ApexPost.Tests;

public sealed class HttpCourierTrackingClientTests
{
    [Fact]
    public async Task Configured_rest_adapter_sends_key_and_parses_the_documented_response()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = CreateClient((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse(ValidResponse("APT / 001")));
        });
        var trackingClient = new HttpCourierTrackingClient(
            client,
            "https://courier.example/root",
            "api/tracking",
            "test-secret");

        var result = await trackingClient.FindShipmentAsync("APT / 001");

        Assert.NotNull(result);
        Assert.Equal("APT / 001", result.TrackingNumber);
        Assert.Equal(ShippingServiceLevel.Express, result.ServiceLevel);
        Assert.Equal(ShipmentType.Parcel, result.ShipmentType);
        Assert.Equal("https://courier.example/root/api/tracking/APT%20%2F%20001", capturedRequest!.RequestUri!.AbsoluteUri);
        Assert.Equal("test-secret", capturedRequest.Headers.GetValues("X-API-Key").Single());
    }

    [Fact]
    public async Task Provider_not_found_is_reported_as_no_tracking_record()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var trackingClient = new HttpCourierTrackingClient(client, "https://courier.example", "tracking", "test-secret");

        var result = await trackingClient.FindShipmentAsync("APT-MISSING");

        Assert.Null(result);
    }

    [Fact]
    public async Task Provider_timeout_is_separate_from_caller_cancellation()
    {
        using var client = CreateClient(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            return JsonResponse(ValidResponse("APT-TEST-001"));
        });
        var timeoutClient = new HttpCourierTrackingClient(
            client, "https://courier.example", "tracking", "test-secret", timeout: TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAsync<TimeoutException>(() => timeoutClient.FindShipmentAsync("APT-TEST-001"));

        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            timeoutClient.FindShipmentAsync("APT-TEST-001", cancel.Token));
    }

    [Fact]
    public async Task Provider_requests_are_bounded_to_the_configured_concurrency()
    {
        var activeRequests = 0;
        var maximumActiveRequests = 0;
        using var client = CreateClient(async (_, token) =>
        {
            var active = Interlocked.Increment(ref activeRequests);
            UpdateMaximum(ref maximumActiveRequests, active);
            try
            {
                await Task.Delay(30, token);
                return JsonResponse(ValidResponse("APT-TEST-001"));
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
            }
        });
        var trackingClient = new HttpCourierTrackingClient(
            client, "https://courier.example", "tracking", "test-secret", maximumConcurrency: 2);

        await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => trackingClient.FindShipmentAsync("APT-TEST-001")));

        Assert.InRange(maximumActiveRequests, 1, 2);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new CallbackHandler(send));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private static string ValidResponse(string trackingNumber) => $$"""
        {
          "trackingNumber": "{{trackingNumber}}",
          "zoneName": "North",
          "serviceLevel": "Express",
          "shipmentType": "Parcel",
          "weightKilograms": 1.25,
          "status": "InTransit",
          "acceptedAt": "2026-10-29T08:30:00+00:00"
        }
        """;

    private static void UpdateMaximum(ref int target, int value)
    {
        var previous = Volatile.Read(ref target);
        while (value > previous)
        {
            var observed = Interlocked.CompareExchange(ref target, value, previous);
            if (observed == previous)
            {
                return;
            }
            previous = observed;
        }
    }

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
