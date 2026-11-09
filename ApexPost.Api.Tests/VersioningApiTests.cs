using System.Net;
using System.Net.Http.Json;
using static ApexPost.Api.Tests.ApiClientExtensions;

namespace ApexPost.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class VersioningApiTests(ApiTestHost host)
{
    private readonly HttpClient client = host.CreateClient();

    [Fact]
    public async Task V2_nests_the_recipient_adds_latest_scan_and_returns_status_as_a_string()
    {
        var trackingNumber = ApiTestHost.NewTrackingNumber();
        using var created = await client.PostAsJsonAsync(ApiRoutes.ShipmentsV2, NewShipment(trackingNumber), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.EndsWith($"/api/v2/shipments/{trackingNumber}", created.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        (await client.PostAsJsonAsync(ApiRoutes.ScansV2(trackingNumber),
            new { scans = new[] { Scan(Guid.NewGuid(), "Pickup", DateTimeOffset.UtcNow.AddMinutes(-5)) } }, Json)).EnsureSuccessStatusCode();

        using var response = await client.GetAsync($"{ApiRoutes.ShipmentsV2}/{trackingNumber}");
        var body = await response.ReadJsonAsync();
        Assert.Equal("PickedUp", body.GetProperty("status").GetString());
        Assert.Equal("Jordan Example", body.GetProperty("recipient").GetProperty("name").GetString());
        Assert.Equal("Maplewood", body.GetProperty("recipient").GetProperty("city").GetString());
        Assert.Equal("Pickup", body.GetProperty("latestScan").GetProperty("scanType").GetString());
        Assert.False(body.TryGetProperty("recipientName", out _));
    }

    [Fact]
    public async Task V1_keeps_the_part_b_contract_and_both_versions_share_the_same_data()
    {
        var trackingNumber = ApiTestHost.NewTrackingNumber();
        (await client.PostAsJsonAsync(ApiRoutes.ShipmentsV2, NewShipment(trackingNumber), Json)).EnsureSuccessStatusCode();

        using var response = await client.GetAsync($"{ApiRoutes.Shipments}/{trackingNumber}");
        var body = await response.ReadJsonAsync();

        Assert.Equal(0, body.GetProperty("status").GetInt32());
        Assert.Equal("Jordan Example", body.GetProperty("recipientName").GetString());
        Assert.False(body.TryGetProperty("latestScan", out _));
    }

    [Fact]
    public async Task Responses_report_supported_and_deprecated_versions_and_v1_has_a_sunset_header()
    {
        using var v1 = await client.GetAsync($"{ApiRoutes.Shipments}?pageSize=1");
        using var v2 = await client.GetAsync($"{ApiRoutes.ShipmentsV2}?pageSize=1");

        Assert.Contains("2", string.Join(",", v2.Headers.GetValues("api-supported-versions")), StringComparison.Ordinal);
        Assert.Contains("1", string.Join(",", v2.Headers.GetValues("api-deprecated-versions")), StringComparison.Ordinal);
        Assert.True(v1.Headers.Contains("Sunset"), "v1 responses should carry a Sunset header.");
    }

    [Fact]
    public async Task Unversioned_part_b_routes_still_work_through_v1()
    {
        var trackingNumber = ApiTestHost.NewTrackingNumber();

        using var created = await client.PostAsJsonAsync(ApiRoutes.LegacyShipments, NewShipment(trackingNumber), Json);
        using var fetched = await client.GetAsync($"{ApiRoutes.LegacyShipments}/{trackingNumber}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(0, (await fetched.ReadJsonAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Openapi_and_swagger_ui_are_not_served_outside_development()
    {
        using var document = await client.GetAsync("/openapi/v2.json");
        using var swagger = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);
    }
}
