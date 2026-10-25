using System.Net;

namespace ApexPost.Web.Middleware;

public sealed class AdminIpRestrictionMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/Administration"))
        {
            var allowed = configuration.GetSection("Security:AdminAllowedIpAddresses").Get<string[]>() ?? [];
            var remoteAddress = context.Connection.RemoteIpAddress;
            if (remoteAddress?.IsIPv4MappedToIPv6 == true)
            {
                remoteAddress = remoteAddress.MapToIPv4();
            }
            var permitted = remoteAddress is not null && allowed.Any(value =>
                IPAddress.TryParse(value, out var address) && address.Equals(remoteAddress));
            if (!permitted)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(context);
    }
}
