using System.Net;
using System.Net.Http.Json;
using System.Text;
using static ApexPost.Api.Tests.ApiClientExtensions;

namespace ApexPost.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ShipmentsApiTests(ApiTestHost host)
{
    private const string Shipments = ApiRoutes.Shipments;
    private readonly HttpClient client = host.CreateClient();

    [Fact]
    public async Task Create_returns_201_with_a_location_header_and_the_shipment()
    {
        var trackingNumber = ApiTestHost.NewTrackingNumber();

        using var response = await client.PostAsJsonAsync(Shipments, NewShipment(trackingNumber), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"{Shipments}/{trackingNumber}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        var body = await response.ReadJsonAsync();
        Assert.Equal(trackingNumber, body.GetProperty("trackingNumber").GetString());

        using var get = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Duplicate_tracking_number_returns_409_problem_details()
    {
        var trackingNumber = ApiTestHost.NewTrackingNumber();
        (await client.PostAsJsonAsync(Shipments, NewShipment(trackingNumber), Json)).EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync(Shipments, NewShipment(trackingNumber), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(409, (await response.ReadJsonAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Invalid_payload_returns_400_validation_problem_details_with_field_errors()
    {
        var invalid = new
        {
            trackingNumber = "APX-123",
            weightKg = 80m,
            recipientName = "",
            recipientPhone = "phone",
            recipientAddress = "",
            city = ""
        };

        using var response = await client.PostAsJsonAsync(Shipments, invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        foreach (var field in new[] { "TrackingNumber", "WeightKg", "RecipientName", "RecipientPhone", "RecipientAddress", "City" })
        {
            Assert.True(HasError(errors, field), $"Expected a validation error for {field}.");
        }
    }

    [Fact]
    public async Task Wrong_content_type_returns_415()
    {
        using var content = new StringContent("trackingNumber=APX-12345678", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(Shipments, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_or_malformed_tracking_numbers_return_404_problem_details()
    {
        using var unknown = await client.GetAsync($"{Shipments}/APX-99999998");
        using var malformed = await client.GetAsync($"{Shipments}/not-a-tracking-number");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task List_is_paged_and_filtered_by_status_and_city()
    {
        var city = $"Testville-{Guid.NewGuid():N}"[..20];
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync(Shipments, NewShipment(ApiTestHost.NewTrackingNumber(), city: city), Json)).EnsureSuccessStatusCode();
        }

        using var response = await client.GetAsync($"{Shipments}?page=1&pageSize=2&status=Created&city={city}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.ReadJsonAsync();
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Page_size_above_50_returns_400()
    {
        using var response = await client.GetAsync($"{Shipments}?pageSize=51");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_replaces_the_editable_details()
    {
        var trackingNumber = await CreateAsync();

        using var response = await client.PutAsJsonAsync($"{Shipments}/{trackingNumber}", new
        {
            weightKg = 4.25m,
            recipientName = "Casey Sample",
            recipientPhone = "+15550100999",
            recipientAddress = "7 Oak Street",
            city = "Riverton"
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(4.25m, body.GetProperty("weightKg").GetDecimal());
    }

    [Fact]
    public async Task Patch_status_follows_the_allowed_transitions_and_rejects_skips_with_409()
    {
        var trackingNumber = await CreateAsync();

        using var ok = await client.PatchAsJsonAsync($"{Shipments}/{trackingNumber}/status", new { status = "PickedUp" }, Json);
        using var skip = await client.PatchAsJsonAsync($"{Shipments}/{trackingNumber}/status", new { status = "Delivered" }, Json);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, skip.StatusCode);
        Assert.Contains("PickedUp", (await skip.ReadJsonAsync()).GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Patch_with_an_unknown_status_returns_400()
    {
        var trackingNumber = await CreateAsync();

        using var response = await client.PatchAsJsonAsync($"{Shipments}/{trackingNumber}/status", new { status = "Teleported" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_204_while_created_and_409_after_pickup()
    {
        var created = await CreateAsync();
        var pickedUp = await CreateAsync();
        (await client.PatchAsJsonAsync($"{Shipments}/{pickedUp}/status", new { status = "PickedUp" }, Json)).EnsureSuccessStatusCode();

        using var deleted = await client.DeleteAsync($"{Shipments}/{created}");
        using var refused = await client.DeleteAsync($"{Shipments}/{pickedUp}");
        using var gone = await client.GetAsync($"{Shipments}/{created}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    private async Task<string> CreateAsync()
    {
        var trackingNumber = ApiTestHost.NewTrackingNumber();
        (await client.PostAsJsonAsync(Shipments, NewShipment(trackingNumber), Json)).EnsureSuccessStatusCode();
        return trackingNumber;
    }

    private static bool HasError(System.Text.Json.JsonElement errors, string field) =>
        errors.EnumerateObject().Any(property => property.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
}
