using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ApexPost.Courier.Core.Resilience;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ApexPost.Courier.Core.Api;

/// <summary>
/// Typed client for the ApexPost API v2, registered with <c>AddHttpClient&lt;ApexApiClient&gt;()</c> (the
/// factory owns the HttpClient - never <c>new HttpClient()</c> per call). The bearer token is attached by
/// <see cref="Auth.AuthHeaderHandler"/>; retries, circuit breaker and timeouts come from the resilience handler.
/// Requests and responses use the source-generated <see cref="CourierJsonContext"/>. Bodies and tokens are never logged.
/// </summary>
public sealed class ApexApiClient(HttpClient http, ILogger<ApexApiClient> logger)
{
    public const string ShipmentsPath = "api/v2/shipments";

    public Task<ShipmentDto> GetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Get,
            $"{ShipmentsPath}/{Uri.EscapeDataString(trackingNumber)}",
            content: null,
            CourierJsonContext.Default.ShipmentDto,
            idempotent: true,
            cancellationToken);

    /// <summary>
    /// Uploads offline scans. Marked idempotent (and therefore retried) although it is a POST: every scan carries
    /// its scan::{Guid} ID and the server skips IDs it already stored (see RESILIENCE.md).
    /// </summary>
    public Task<ScanUploadResultDto> UploadScansAsync(
        string trackingNumber,
        ScanUploadRequestDto request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"{ShipmentsPath}/{Uri.EscapeDataString(trackingNumber)}/scans",
            JsonContent.Create(request, CourierJsonContext.Default.ScanUploadRequestDto),
            CourierJsonContext.Default.ScanUploadResultDto,
            idempotent: true,
            cancellationToken);

    /// <summary>Development only: asks the API's /dev/token endpoint for a Courier token.</summary>
    public Task<DevTokenDto> RequestDevelopmentTokenAsync(string courierId, CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Get,
            $"dev/token?roles=Courier&courierId={Uri.EscapeDataString(courierId)}",
            content: null,
            CourierJsonContext.Default.DevTokenDto,
            idempotent: true,
            cancellationToken);

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativeUri,
        HttpContent? content,
        JsonTypeInfo<T> responseType,
        bool idempotent,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUri) { Content = content };
        request.Options.Set(CourierResilience.IdempotentRequest, idempotent);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (BrokenCircuitException exception)
        {
            throw new ApiUnavailableException(CourierResilience.OfflineBanner, exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new ApiUnavailableException("The server did not answer in time. Working offline.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ApiUnavailableException("No connection to the server. Working offline.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiUnavailableException("The request timed out. Working offline.", exception);
        }

        using (response)
        {
            logger.LogInformation("{Method} {Path} -> {Status}", method, request.RequestUri?.AbsolutePath, (int)response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return await response.Content.ReadFromJsonAsync(responseType, cancellationToken)
                        ?? throw new ApiMalformedResponseException("The server sent an empty response.");
                }
                catch (JsonException exception)
                {
                    throw new ApiMalformedResponseException("The server sent a response the app could not read.", exception);
                }
                catch (NotSupportedException exception)
                {
                    throw new ApiMalformedResponseException("The server sent an unexpected content type.", exception);
                }
            }

            var problem = await TryReadProblemAsync(response, cancellationToken);
            if (CourierResilience.IsTransient(response.StatusCode))
            {
                throw new ApiUnavailableException("The server is having problems. Working offline.");
            }

            throw new ApiProblemException(response.StatusCode, problem, ProblemMessages.Friendly(response.StatusCode, problem));
        }
    }

    private static async Task<ProblemDetailsDto?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            if (response.Content.Headers.ContentLength == 0)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync(CourierJsonContext.Default.ProblemDetailsDto, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Not Problem Details (for example an HTML error page from a proxy): fall back to the status code.
            return null;
        }
    }
}

/// <summary>Turns Problem Details into short, friendly text for the courier.</summary>
public static class ProblemMessages
{
    public static string Friendly(HttpStatusCode status, ProblemDetailsDto? problem)
    {
        if (problem?.Errors is { Count: > 0 } errors)
        {
            var first = errors
                .SelectMany(error => error.Value.Select(message => message))
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Take(3);
            return "Please check: " + string.Join(" ", first);
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            HttpStatusCode.Forbidden => problem?.Detail ?? "You are not allowed to do that.",
            HttpStatusCode.NotFound => "That tracking number is not known to the server.",
            HttpStatusCode.Conflict => problem?.Detail ?? "The shipment was changed by someone else. Refresh and try again.",
            _ => problem?.Detail ?? problem?.Title ?? $"The server rejected the request ({(int)status})."
        };
    }
}
