using System.Security.Cryptography;
using System.Text;

namespace ApexPost.Web.Middleware;

public sealed class PartnerApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/partner") && !HttpMethods.IsOptions(context.Request.Method))
        {
            var expectedKey = configuration["PartnerApi:ApiKey"];
            var providedKey = context.Request.Headers["X-API-Key"].ToString();
            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { error = "Partner API is not configured." });
                return;
            }

            var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
            var providedBytes = Encoding.UTF8.GetBytes(providedKey);
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await next(context);
    }
}
