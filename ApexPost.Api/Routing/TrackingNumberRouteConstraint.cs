using ApexPost.Domain.Courier;

namespace ApexPost.Api.Routing;

/// <summary>
/// Route constraint <c>{trackingNumber:trackingNumber}</c>: only APX-######## matches, so malformed numbers never
/// reach a controller (they get 404 Not Found as Problem Details).
/// </summary>
public sealed class TrackingNumberRouteConstraint : IRouteConstraint
{
    public const string Name = "trackingNumber";

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection) =>
        values.TryGetValue(routeKey, out var value) &&
        CourierShipment.IsValidTrackingNumber(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
}
