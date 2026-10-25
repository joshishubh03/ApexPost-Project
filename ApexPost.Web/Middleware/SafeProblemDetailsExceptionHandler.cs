using System.Diagnostics;
using System.Text.Json;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Middleware;

public sealed class SafeProblemDetailsExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "The request could not be completed. Use the trace ID when contacting support.",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json; charset=utf-8";
        httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem, JsonOptions, cancellationToken);
        return true;
    }
}
