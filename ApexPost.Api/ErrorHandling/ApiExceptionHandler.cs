using System.Diagnostics;
using ApexPost.Application.Courier;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Courier;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.ErrorHandling;

/// <summary>
/// Global exception handler: every unhandled exception becomes RFC 9457 Problem Details.
/// <list type="table">
/// <item><term>404</term><description>unknown tracking number (<see cref="ShipmentNotFoundException"/>)</description></item>
/// <item><term>409</term><description>duplicate tracking number, invalid status transition, delete outside Created, concurrency conflict</description></item>
/// <item><term>403</term><description>a courier uploading scans under another courier's ID</description></item>
/// <item><term>400</term><description>a domain rule rejected a value (ArgumentException from Domain/Application) or a malformed request</description></item>
/// <item><term>500</term><description>anything else - generic message; exception type and stack trace only in Development</description></item>
/// </list>
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly System.Reflection.Assembly[] BusinessAssemblies =
    [
        typeof(CourierShipment).Assembly,
        typeof(CourierShipmentService).Assembly
    ];

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = Classify(exception);
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("{Status} for {Method} {Path}: {Title}", status, httpContext.Request.Method, httpContext.Request.Path, title);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = TypeFor(status),
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        // Stack traces and exception types never leave the server outside Development.
        if (environment.IsDevelopment() && status >= StatusCodes.Status500InternalServerError)
        {
            problem.Extensions["exception"] = new
            {
                type = exception.GetType().FullName,
                message = exception.Message,
                stackTrace = exception.StackTrace
            };
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    public static (int Status, string Title, string Detail) Classify(Exception exception) => exception switch
    {
        ShipmentNotFoundException notFound =>
            (StatusCodes.Status404NotFound, "Shipment not found.", notFound.Message),
        KeyNotFoundException =>
            (StatusCodes.Status404NotFound, "Resource not found.", "The requested resource could not be found."),
        DuplicateResourceException duplicate =>
            (StatusCodes.Status409Conflict, "Duplicate tracking number.", duplicate.Message),
        InvalidShipmentStatusTransitionException transition =>
            (StatusCodes.Status409Conflict, "Invalid status transition.", transition.Message),
        ShipmentRuleViolationException rule =>
            (StatusCodes.Status409Conflict, "The shipment's current status does not allow this operation.", rule.Message),
        ConcurrencyConflictException conflict =>
            (StatusCodes.Status409Conflict, "The shipment was changed by another request.", conflict.Message),
        ScanOwnershipException ownership =>
            (StatusCodes.Status403Forbidden, "Scans can only be uploaded under your own courier ID.", ownership.Message),
        BadHttpRequestException badRequest =>
            (badRequest.StatusCode, "The request could not be read.", "The request body or headers are malformed."),
        ArgumentException argument when IsFromBusinessCode(argument) =>
            (StatusCodes.Status400BadRequest, "The request violates a business rule.", argument.Message),
        _ =>
            (StatusCodes.Status500InternalServerError, "An unexpected error occurred.",
                "The request could not be completed. Quote the traceId when contacting support.")
    };

    private static string TypeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };

    private static bool IsFromBusinessCode(Exception exception)
    {
        var origin = exception.TargetSite?.DeclaringType;
        while (origin?.DeclaringType is not null)
        {
            origin = origin.DeclaringType;
        }

        return origin is not null && BusinessAssemblies.Contains(origin.Assembly);
    }
}
