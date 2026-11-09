namespace ApexPost.Api.Tests;

internal static class ApiRoutes
{
    public const string Shipments = "/api/shipments";

    public static string Scans(string trackingNumber) => $"{Shipments}/{trackingNumber}/scans";
}
