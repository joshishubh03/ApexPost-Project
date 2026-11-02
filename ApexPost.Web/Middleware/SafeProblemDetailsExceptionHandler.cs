using System.Diagnostics;
using System.Text.Json;
using ApexPost.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Maps unhandled exceptions to RFC 7807 Problem Details without leaking internals:
///   ConcurrencyConflictException (DbUpdateConcurrencyException)  -> 409
///   DuplicateResourceException (unique-constraint violation)    -> 409
///   KeyNotFoundException (not found)                            -> 404
///   ArgumentException / InvalidOperationException raised by the Domain or Application layer -> 400
///   anything else                                               -> 500 (generic message only)
/// </summary>
public sealed class SafeProblemDetailsExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly System.Reflection.Assembly[] BusinessAssemblies =
    [
        typeof(ApexPost.Domain.Entities.Branch).Assembly,
        typeof(ApexPost.Application.Services.ReportingService).Assembly
    ];

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var (status, title, detail) = Classify(exception);
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                _ => "about:blank"
            },
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json; charset=utf-8";
        httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem, JsonOptions, cancellationToken);
        return true;
    }

    public static (int Status, string Title, string Detail) Classify(Exception exception) => exception switch
    {
        ConcurrencyConflictException conflict =>
            (StatusCodes.Status409Conflict, "The record was changed by another user.", conflict.Message),
        DuplicateResourceException duplicate =>
            (StatusCodes.Status409Conflict, "A record with the same unique value already exists.", duplicate.Message),
        KeyNotFoundException =>
            (StatusCodes.Status404NotFound, "The requested resource was not found.", "The requested resource could not be found."),
        ArgumentException or InvalidOperationException when IsBusinessRuleViolation(exception) =>
            (StatusCodes.Status400BadRequest, "The request violates a business rule.", exception.Message),
        _ =>
            (StatusCodes.Status500InternalServerError, "An unexpected error occurred.",
                "The request could not be completed. Use the trace ID when contacting support.")
    };

    private static bool IsBusinessRuleViolation(Exception exception)
    {
        var origin = exception.TargetSite?.DeclaringType;
        while (origin?.DeclaringType is not null)
        {
            origin = origin.DeclaringType;
        }

        return origin is not null && BusinessAssemblies.Contains(origin.Assembly);
    }
}
