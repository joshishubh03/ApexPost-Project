namespace ApexPost.Api.Tests;

internal static class ApiRoutes
{
    /// <summary>v1 (deprecated) - the Part B contract.</summary>
    public const string Shipments = "/api/v1/shipments";

    public const string ShipmentsV2 = "/api/v2/shipments";

    /// <summary>Unversioned Part B route, rewritten to v1 until the v1 sunset.</summary>
    public const string LegacyShipments = "/api/shipments";

    public static string Scans(string trackingNumber) => $"{Shipments}/{trackingNumber}/scans";

    public static string ScansV2(string trackingNumber) => $"{ShipmentsV2}/{trackingNumber}/scans";
}
