namespace ApexPost.Web.Middleware;

public sealed class MaintenanceModeMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var enabled = configuration.GetValue<bool>("Security:MaintenanceMode");
        if (enabled && !context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            var retryAfterSeconds = Math.Clamp(
                configuration.GetValue<int?>("Security:MaintenanceRetryAfterSeconds") ?? 300,
                1,
                86_400);
            context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "about:blank",
                title = "Service temporarily unavailable",
                status = StatusCodes.Status503ServiceUnavailable,
                detail = "ApexPost is in scheduled maintenance."
            });
            return;
        }

        await next(context);
    }
}
