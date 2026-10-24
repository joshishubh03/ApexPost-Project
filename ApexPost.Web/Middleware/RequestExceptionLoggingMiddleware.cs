namespace ApexPost.Web.Middleware;

/// <summary>
/// Logs unhandled request failures with their correlation ID, then lets ASP.NET's
/// configured exception handler create the response.
/// </summary>
public sealed class RequestExceptionLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestExceptionLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Unhandled request failure for {Method} {Path} (correlation {CorrelationId}).",
                context.Request.Method,
                context.Request.Path,
                CorrelationIdMiddleware.GetCorrelationId(context));
            throw;
        }
    }
}
