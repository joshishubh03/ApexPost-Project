using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;

namespace ApexPost.Infrastructure;

/// <summary>
/// Generic REST adapter. The provider must return the documented ShipmentTrackingSummary JSON shape.
/// </summary>
public sealed class HttpCourierTrackingClient : ICourierTrackingClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient httpClient;
    private readonly string trackingPath;
    private readonly string apiKey;
    private readonly SemaphoreSlim concurrencyGate;
    private readonly TimeSpan timeout;

    public HttpCourierTrackingClient(
        HttpClient httpClient,
        string baseAddress,
        string trackingPath,
        string apiKey,
        int maximumConcurrency = 3,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Courier provider base address must be an absolute HTTPS URL.", nameof(baseAddress));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (maximumConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        }

        this.httpClient = httpClient;
        this.httpClient.BaseAddress = EnsureTrailingSlash(uri);
        this.trackingPath = trackingPath.Trim('/');
        this.apiKey = apiKey;
        concurrencyGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        this.timeout = timeout ?? TimeSpan.FromSeconds(2);
        if (this.timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    public async Task<ShipmentTrackingSummary?> FindShipmentAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var enteredGate = false;

        try
        {
            await concurrencyGate.WaitAsync(timeoutSource.Token);
            enteredGate = true;
            var relativePath = $"{trackingPath}/{Uri.EscapeDataString(trackingNumber.Trim())}";
            using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
            request.Headers.Add("X-API-Key", apiKey);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();

            ShipmentTrackingSummary? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<ShipmentTrackingSummary>(
                    JsonOptions,
                    timeoutSource.Token);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The courier tracking response could not be parsed.", exception);
            }
            return ValidateResponse(result, trackingNumber);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The courier tracking lookup exceeded its configured timeout.");
        }
        finally
        {
            if (enteredGate)
            {
                concurrencyGate.Release();
            }
        }
    }

    private static ShipmentTrackingSummary? ValidateResponse(ShipmentTrackingSummary? result, string requestedNumber)
    {
        if (result is null ||
            string.IsNullOrWhiteSpace(result.TrackingNumber) ||
            string.IsNullOrWhiteSpace(result.ZoneName) ||
            string.IsNullOrWhiteSpace(result.Status) ||
            result.WeightKilograms < 0 ||
            !Enum.IsDefined(result.ServiceLevel) ||
            (result.ShipmentType is { } shipmentType && !Enum.IsDefined(shipmentType)))
        {
            throw new InvalidDataException("The courier tracking response did not match the configured response contract.");
        }

        if (!string.Equals(result.TrackingNumber, requestedNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The courier tracking response did not match the requested tracking number.");
        }

        return result;
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
