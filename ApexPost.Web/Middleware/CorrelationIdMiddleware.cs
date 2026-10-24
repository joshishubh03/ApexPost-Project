namespace ApexPost.Web.Middleware;

/// <summary>
/// Gives every HTTP request a safe correlation ID and returns it to the caller.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const string ItemKey = "ApexPost.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.TryParse(context.Request.Headers[HeaderName], out var suppliedId)
            ? suppliedId.ToString("D")
            : Guid.NewGuid().ToString("D");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;
}
