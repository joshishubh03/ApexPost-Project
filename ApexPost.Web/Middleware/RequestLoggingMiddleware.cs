using System.Diagnostics;

namespace ApexPost.Web.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var escapedExceptionStatusCode = 0;

        try
        {
            await next(context);
        }
        catch
        {
            escapedExceptionStatusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = escapedExceptionStatusCode == 0
                ? context.Response.StatusCode
                : escapedExceptionStatusCode;
            logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms.",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
