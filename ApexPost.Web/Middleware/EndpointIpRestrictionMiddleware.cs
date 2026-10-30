using System.Net;

namespace ApexPost.Web.Middleware;

public sealed class EndpointIpRestrictionMiddleware(RequestDelegate next, IConfiguration configuration)
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
        var integrationEndpoint = context.GetEndpoint()?.Metadata
            .GetMetadata<ProtectedIntegrationEndpointAttribute>() is not null;

        var allowlistSection = administrativePath
            ? "Security:AdminAllowedIpAddresses"
            : integrationEndpoint
                ? "Security:IntegrationAllowedIpAddresses"
                : null;

        if (allowlistSection is not null)
        {
            var allowedAddresses = configuration.GetSection(allowlistSection).Get<string[]>() ?? [];
            var remoteAddress = context.Connection.RemoteIpAddress;
            if (remoteAddress is not null)
            {
                remoteAddress = Normalize(remoteAddress);
            }

            var permitted = remoteAddress is not null && allowedAddresses.Any(value =>
                IPAddress.TryParse(value, out var allowedAddress) &&
                Normalize(allowedAddress).Equals(remoteAddress));
            if (!permitted)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(context);
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
