using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using static ApexPost.Api.Tests.ApiClientExtensions;

namespace ApexPost.Api.Tests;

/// <summary>Part D: 401 for missing/invalid/expired tokens, 403 for wrong roles and foreign courier IDs, partner API keys.</summary>
[Collection(ApiCollection.Name)]
public sealed class SecurityApiTests(ApiTestHost host)
{
    [Fact]
    public async Task Missing_token_is_401_problem_details()
    {
        using var client = host.CreateAnonymousClient();

        using var response = await client.GetAsync($"{ApiRoutes.ShipmentsV2}?pageSize=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.c2lnbmF0dXJl")]
    public async Task Invalid_token_is_401(string token)
    {
        using var client = host.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync($"{ApiRoutes.ShipmentsV2}?pageSize=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_401_with_an_explanation()
    {
        using var client = host.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTestHost.Token("Courier", expired: true));

        using var response = await client.GetAsync($"{ApiRoutes.ShipmentsV2}?pageSize=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("expired", (await response.ReadJsonAsync()).GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_401()
    {
        var foreign = new Api.Security.DevelopmentTokenIssuer(
            new Api.Security.JwtSettings
            {
                Issuer = ApiTestHost.Issuer,
                Audience = ApiTestHost.Audience,
                SigningKey = Convert.ToBase64String(new byte[32])
            },
            TimeProvider.System).Issue(["Admin"], null).AccessToken;
        using var client = host.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", foreign);

        using var response = await client.GetAsync($"{ApiRoutes.ShipmentsV2}?pageSize=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Any_authenticated_user_can_read()
    {
        using var courier = host.CreateAuthenticatedClient("Courier");

        using var response = await courier.GetAsync($"{ApiRoutes.ShipmentsV2}?pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_requires_the_admin_role()
    {
        var trackingNumber = await CreateAsync();
        using var courier = host.CreateAuthenticatedClient("Courier");
        using var admin = host.CreateAuthenticatedClient("Admin", courierId: null);

        using var forbidden = await courier.DeleteAsync($"{ApiRoutes.ShipmentsV2}/{trackingNumber}");
        using var deleted = await admin.DeleteAsync($"{ApiRoutes.ShipmentsV2}/{trackingNumber}");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Status_patch_and_scan_upload_require_the_courier_role()
    {
        var trackingNumber = await CreateAsync();
        using var admin = host.CreateAuthenticatedClient("Admin", courierId: null);

        using var patch = await admin.PatchAsJsonAsync($"{ApiRoutes.ShipmentsV2}/{trackingNumber}/status", new { status = "PickedUp" }, Json);
        using var upload = await admin.PostAsJsonAsync(ApiRoutes.ScansV2(trackingNumber),
            new { scans = new[] { Scan(Guid.NewGuid(), "Pickup", DateTimeOffset.UtcNow.AddMinutes(-1)) } }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }

    [Fact]
    public async Task A_courier_cannot_upload_scans_under_another_courier_id()
    {
        var trackingNumber = await CreateAsync();
        using var courier = host.CreateAuthenticatedClient("Courier", courierId: "courier-007");

        using var own = await courier.PostAsJsonAsync(ApiRoutes.ScansV2(trackingNumber),
            new { scans = new[] { Scan(Guid.NewGuid(), "Pickup", DateTimeOffset.UtcNow.AddMinutes(-2), courierId: "courier-007") } }, Json);
        using var foreign = await courier.PostAsJsonAsync(ApiRoutes.ScansV2(trackingNumber),
            new { scans = new[] { Scan(Guid.NewGuid(), "InTransit", DateTimeOffset.UtcNow.AddMinutes(-1), courierId: "courier-999") } }, Json);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal("application/problem+json", foreign.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Partner_route_accepts_a_valid_api_key_and_returns_no_recipient_details()
    {
        var trackingNumber = await CreateAsync();
        using var partner = host.CreateAnonymousClient();
        partner.DefaultRequestHeaders.Add("X-Api-Key", ApiTestHost.PartnerKey);

        using var response = await partner.GetAsync($"/api/v2/partner/shipments/{trackingNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("Created", body.GetProperty("status").GetString());
        Assert.Equal("Maplewood", body.GetProperty("destinationCity").GetString());
        Assert.False(body.TryGetProperty("recipient", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task Partner_route_rejects_a_missing_or_invalid_api_key_with_401(string? key)
    {
        using var partner = host.CreateAnonymousClient();
        if (key is not null)
        {
            partner.DefaultRequestHeaders.Add("X-Api-Key", key);
        }

        using var response = await partner.GetAsync("/api/v2/partner/shipments/APX-12345678");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Partner_key_is_not_a_bearer_token_for_the_courier_api()
    {
        using var partner = host.CreateAnonymousClient();
        partner.DefaultRequestHeaders.Add("X-Api-Key", ApiTestHost.PartnerKey);

        using var response = await partner.GetAsync($"{ApiRoutes.ShipmentsV2}?pageSize=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> CreateAsync()
    {
        using var admin = host.CreateAuthenticatedClient("Admin", courierId: null);
        var trackingNumber = ApiTestHost.NewTrackingNumber();
        (await admin.PostAsJsonAsync(ApiRoutes.ShipmentsV2, NewShipment(trackingNumber), Json)).EnsureSuccessStatusCode();
        return trackingNumber;
    }
}
