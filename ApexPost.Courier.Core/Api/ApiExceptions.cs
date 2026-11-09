using System.Net;

namespace ApexPost.Courier.Core.Api;

/// <summary>Base type for API failures. <see cref="UserMessage"/> is safe to show on screen.</summary>
public abstract class ApiException(string userMessage, Exception? innerException = null)
    : Exception(userMessage, innerException)
{
    public string UserMessage { get; } = userMessage;
}

/// <summary>
/// The server could not be reached or answered with a transient error after the resilience pipeline gave up
/// (network error, timeout, 408/5xx, circuit open). Work continues offline; scans stay Pending.
/// </summary>
public sealed class ApiUnavailableException(string userMessage, Exception? innerException = null)
    : ApiException(userMessage, innerException);

/// <summary>The server answered with a 4xx Problem Details response.</summary>
public sealed class ApiProblemException(HttpStatusCode statusCode, ProblemDetailsDto? problem, string userMessage)
    : ApiException(userMessage)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public ProblemDetailsDto? Problem { get; } = problem;
}

/// <summary>A success response whose body could not be understood (malformed JSON, wrong shape).</summary>
public sealed class ApiMalformedResponseException(string userMessage, Exception? innerException = null)
    : ApiException(userMessage, innerException);
