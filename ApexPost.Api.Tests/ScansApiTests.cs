using System.Net;
using System.Net.Http.Json;
using static ApexPost.Api.Tests.ApiClientExtensions;

namespace ApexPost.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ScansApiTests(ApiTestHost host)
{
    private readonly HttpClient client = host.CreateAuthenticatedClient();

    [Fact]
    public async Task Uploading_the_same_scan_ids_twice_never_creates_duplicates()
    {
        var trackingNumber = await CreateAsync();
        var at = DateTimeOffset.UtcNow.AddMinutes(-30);
        var request = new
        {
            scans = new[]
            {
                Scan(Guid.NewGuid(), "Pickup", at),
                Scan(Guid.NewGuid(), "InTransit", at.AddMinutes(10))
            }
        };

        using var first = await client.PostAsJsonAsync(ApiRoutes.Scans(trackingNumber), request, Json);
        using var second = await client.PostAsJsonAsync(ApiRoutes.Scans(trackingNumber), request, Json);

        var firstBody = await first.ReadJsonAsync();
        var secondBody = await second.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2, firstBody.GetProperty("accepted").GetInt32());
        Assert.Equal(0, firstBody.GetProperty("duplicatesSkipped").GetInt32());
        Assert.Equal(0, secondBody.GetProperty("accepted").GetInt32());
        Assert.Equal(2, secondBody.GetProperty("duplicatesSkipped").GetInt32());

        using var history = await client.GetAsync(ApiRoutes.Scans(trackingNumber));
        var scans = await history.ReadJsonAsync();
        Assert.Equal(2, scans.GetArrayLength());
        Assert.Equal("Pickup", scans[0].GetProperty("scanType").GetString());

        using var shipment = await client.GetAsync($"{ApiRoutes.Shipments}/{trackingNumber}");
        Assert.Equal(2, (await shipment.ReadJsonAsync()).GetProperty("status").GetInt32()); // InTransit
    }

    [Fact]
    public async Task Scans_that_break_the_status_machine_are_rejected_with_a_reason()
    {
        var trackingNumber = await CreateAsync();
        var rejectedId = Guid.NewGuid();
        var request = new
        {
            scans = new[]
            {
                Scan(Guid.NewGuid(), "Pickup", DateTimeOffset.UtcNow.AddMinutes(-20)),
                Scan(rejectedId, "Delivered", DateTimeOffset.UtcNow.AddMinutes(-10), signature: "J. Doe")
            }
        };

        using var response = await client.PostAsJsonAsync(ApiRoutes.Scans(trackingNumber), request, Json);

        var body = await response.ReadJsonAsync();
        Assert.Equal(1, body.GetProperty("accepted").GetInt32());
        var rejected = Assert.Single(body.GetProperty("rejected").EnumerateArray());
        Assert.Equal($"scan::{rejectedId:D}", rejected.GetProperty("scanId").GetString());
        Assert.Contains("PickedUp", rejected.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scan_in_the_future_is_a_400_validation_error()
    {
        var trackingNumber = await CreateAsync();

        using var response = await client.PostAsJsonAsync(ApiRoutes.Scans(trackingNumber),
            new { scans = new[] { Scan(Guid.NewGuid(), "Pickup", DateTimeOffset.UtcNow.AddDays(1)) } }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("future", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_delivered_scan_without_a_signature_is_a_400_validation_error()
    {
        var trackingNumber = await CreateAsync();

        using var response = await client.PostAsJsonAsync(ApiRoutes.Scans(trackingNumber),
            new { scans = new[] { Scan(Guid.NewGuid(), "Delivered", DateTimeOffset.UtcNow.AddMinutes(-1)) } }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("signature", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scans_for_an_unknown_shipment_return_404()
    {
        using var response = await client.PostAsJsonAsync(ApiRoutes.Scans("APX-99999997"),
            new { scans = new[] { Scan(Guid.NewGuid(), "Pickup", DateTimeOffset.UtcNow.AddMinutes(-1)) } }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<string> CreateAsync()
    {
        var trackingNumber = ApiTestHost.NewTrackingNumber();
        (await client.PostAsJsonAsync(ApiRoutes.Shipments, NewShipment(trackingNumber), Json)).EnsureSuccessStatusCode();
        return trackingNumber;
    }
}
