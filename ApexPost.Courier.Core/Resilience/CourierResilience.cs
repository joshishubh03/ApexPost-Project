using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ApexPost.Courier.Core.Resilience;

/// <summary>Tunable values for the API resilience pipeline (defaults explained in RESILIENCE.md).</summary>
public sealed class CourierResilienceOptions
{
    /// <summary>Retries after the first attempt (so up to 4 attempts in total).</summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>Base delay of the exponential back-off (1 s, 2 s, 4 s ... plus jitter).</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Timeout of one attempt.</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Timeout of the whole request including all retries and delays.</summary>
    public TimeSpan TotalRequestTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>The circuit opens when at least this share of calls fail within the sampling window...</summary>
    public double CircuitFailureRatio { get; init; } = 0.5;

    /// <summary>...and at least this many calls were made in the window.</summary>
    public int CircuitMinimumThroughput { get; init; } = 4;

    public TimeSpan CircuitSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long the circuit stays open before one trial call (half-open).</summary>
    public TimeSpan CircuitBreakDuration { get; init; } = TimeSpan.FromSeconds(20);
}

/// <summary>
/// Polly v8 pipeline for the ApexPost API (via Microsoft.Extensions.Http.Resilience), outermost first:
/// total timeout -> retry (exponential back-off + jitter) -> circuit breaker -> per-attempt timeout.
/// Retries happen only for network errors, timeouts, 408 and 5xx - never for other 4xx - and only for idempotent
/// requests. Every retry and every circuit state change is logged.
/// </summary>
public static class CourierResilience
{
    public const string PipelineName = "apexpost-api";
    public const string OfflineBanner = "Server unavailable – working offline";

    /// <summary>Request option: true when the request is safe to repeat (GET, or a scan upload with scan IDs).</summary>
    public static readonly HttpRequestOptionsKey<bool> IdempotentRequest = new("ApexPost.Idempotent");

    public static IHttpClientBuilder AddCourierResilience(this IHttpClientBuilder builder, CourierResilienceOptions? options = null)
    {
        var settings = options ?? new CourierResilienceOptions();
        builder.AddResilienceHandler(PipelineName, (pipeline, context) =>
        {
            var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ApexPost.Courier.Resilience");
            var circuit = context.ServiceProvider.GetRequiredService<ApiCircuitMonitor>();

            pipeline.AddTimeout(new TimeoutStrategyOptions
            {
                Name = "total-request-timeout",
                Timeout = settings.TotalRequestTimeout
            });

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                Name = "retry",
                MaxRetryAttempts = settings.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = settings.RetryBaseDelay,
                ShouldHandle = arguments => ValueTask.FromResult(
                    IsIdempotent(arguments.Outcome.Result?.RequestMessage ?? arguments.Context.GetRequestMessage()) &&
                    IsTransient(arguments.Outcome)),
                OnRetry = arguments =>
                {
                    logger.LogWarning(
                        "Retry {Attempt}/{Max} in {Delay} ms after {Outcome}",
                        arguments.AttemptNumber + 1,
                        settings.MaxRetryAttempts,
                        (int)arguments.RetryDelay.TotalMilliseconds,
                        Describe(arguments.Outcome));
                    return default;
                }
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                Name = "circuit-breaker",
                FailureRatio = settings.CircuitFailureRatio,
                MinimumThroughput = settings.CircuitMinimumThroughput,
                SamplingDuration = settings.CircuitSamplingDuration,
                BreakDuration = settings.CircuitBreakDuration,
                ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome)),
                OnOpened = arguments =>
                {
                    logger.LogWarning("Circuit OPEN for {Seconds} s after {Outcome} - working offline",
                        (int)arguments.BreakDuration.TotalSeconds, Describe(arguments.Outcome));
                    circuit.Set(ApiCircuitState.Open);
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation("Circuit HALF-OPEN - sending one trial request");
                    circuit.Set(ApiCircuitState.HalfOpen);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Circuit CLOSED - server reachable again");
                    circuit.Set(ApiCircuitState.Closed);
                    return default;
                }
            });

            pipeline.AddTimeout(new TimeoutStrategyOptions
            {
                Name = "attempt-timeout",
                Timeout = settings.AttemptTimeout
            });
        });

        return builder;
    }

    /// <summary>Network errors, timeouts, 408 and 5xx. Every other status (including all other 4xx) is final.</summary>
    public static bool IsTransient(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: HttpRequestException or TimeoutRejectedException } => true,
        { Exception: not null } => false,
        { Result: { } response } => IsTransient(response.StatusCode),
        _ => false
    };

    public static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout || (int)statusCode >= 500;

    public static bool IsIdempotent(HttpRequestMessage? request)
    {
        if (request is null)
        {
            return false;
        }

        if (request.Options.TryGetValue(IdempotentRequest, out var flagged))
        {
            return flagged;
        }

        return request.Method == HttpMethod.Get || request.Method == HttpMethod.Head ||
               request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete ||
               request.Method == HttpMethod.Options;
    }

    private static string Describe(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is { } exception
            ? exception.GetType().Name
            : $"HTTP {(int?)outcome.Result?.StatusCode}";
}

public enum ApiCircuitState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Shared view of the circuit breaker for the UI: while it is open the app shows
/// "Server unavailable – working offline" and keeps saving scans locally.
/// </summary>
public sealed class ApiCircuitMonitor
{
    private int state = (int)ApiCircuitState.Closed;

    public event EventHandler<ApiCircuitState>? StateChanged;

    public ApiCircuitState State => (ApiCircuitState)Volatile.Read(ref state);

    public bool IsServerUnavailable => State == ApiCircuitState.Open;

    public DateTimeOffset? LastChangedUtc { get; private set; }

    public void Set(ApiCircuitState newState)
    {
        if (Interlocked.Exchange(ref state, (int)newState) != (int)newState)
        {
            LastChangedUtc = DateTimeOffset.UtcNow;
            StateChanged?.Invoke(this, newState);
        }
    }
}
