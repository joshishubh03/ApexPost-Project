using System.Net;

namespace ApexPost.Web.Middleware;

public sealed class AdminIpRestrictionMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var administrativePath = path.StartsWithSegments("/Administration") ||
            (HttpMethods.IsPost(context.Request.Method) &&
                (path.StartsWithSegments("/Catalog/Create") ||
                 path.StartsWithSegments("/Catalog/SetActive") ||
                 path.StartsWithSegments("/Inventory/Adjust") ||
                 path.StartsWithSegments("/Shipping/CreateZone") ||
                 path.StartsWithSegments("/Shipping/CreateRate")));

        if (administrativePath)
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
