using System.Net;
using System.Text;
using System.Text.Json;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Auth;

namespace ApexPost.Courier.Core.Tests;

public sealed class AuthAndJsonTests
{
    [Fact]
    public async Task Auth_header_handler_attaches_the_bearer_token()
    {
        var handler = new StubHandler((_, _) => StubHandler.Json(HttpStatusCode.OK, TestApi.ShipmentJson));
        using var api = new TestApi(handler, token: "abc.def.ghi");

        await api.Client.GetShipmentAsync("APX-10000001");

        Assert.Equal("Bearer abc.def.ghi", Assert.Single(handler.AuthorizationHeaders));
    }

    [Fact]
    public async Task A_401_clears_the_token_and_signals_sign_out_without_retrying()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var api = new TestApi(handler);
        string? reason = null;
        api.Session.SignedOut += (_, message) => reason = message;

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Null(await api.Tokens.GetAccessTokenAsync());
        Assert.NotNull(reason);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Source_generated_context_tolerates_unknown_and_null_fields()
    {
        const string json = """
            {"trackingNumber":"APX-10000001","status":"Delivered","weightKg":1.5,"recipient":null,
             "latestScan":null,"somethingNew":{"nested":[1,2,3]},"createdAtUtc":"2026-11-09T07:00:00Z"}
            """;

        var shipment = JsonSerializer.Deserialize(json, CourierJsonContext.Default.ShipmentDto)!;

        Assert.Equal(ShipmentStatusDto.Delivered, shipment.Status);
        Assert.Null(shipment.Recipient);
        Assert.Null(shipment.LatestScan);
    }

    [Fact]
    public void Shared_options_use_camel_case_and_string_enums()
    {
        var json = JsonSerializer.Serialize(new ScanUploadRequestDto
        {
            Scans =
            [
                new ScanUploadItemDto
                {
                    ScanId = "scan::3f2c9a5e-0b1d-4c7e-9a10-2b3c4d5e6f70",
                    ScanType = "Pickup",
                    CourierId = "courier-007",
                    Location = "Depot",
                    ScannedAtUtc = new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero)
                }
            ]
        }, CourierJsonContext.Default.ScanUploadRequestDto);

        Assert.Contains("\"scanId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"notes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Delivered\"", JsonSerializer.Serialize(ShipmentStatusDto.Delivered, CourierJson.Options), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_success_body_is_a_friendly_error_not_a_crash()
    {
        var handler = new StubHandler((_, _) => StubHandler.Json(HttpStatusCode.OK, "<html>proxy login</html>"));
        using var api = new TestApi(handler);

        var exception = await Assert.ThrowsAsync<ApiMalformedResponseException>(() => api.Client.GetShipmentAsync("APX-10000001"));

        Assert.False(string.IsNullOrWhiteSpace(exception.UserMessage));
    }

    [Fact]
    public async Task Validation_problem_details_become_a_friendly_message()
    {
        var handler = new StubHandler((_, _) => StubHandler.Json(HttpStatusCode.BadRequest, """
            {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.",
             "status":400,"errors":{"Scans[0].SignatureName":["A Delivered scan requires the recipient's signature name."]}}
            """, "application/problem+json"));
        using var api = new TestApi(handler);

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() =>
            api.Client.UploadScansAsync("APX-10000001", new ScanUploadRequestDto()));

        Assert.Contains("signature name", exception.UserMessage, StringComparison.Ordinal);
        Assert.NotNull(exception.Problem?.Errors);
    }

    [Fact]
    public void Jwt_reader_extracts_courier_id_roles_and_expiry_without_validating()
    {
        static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = $"{Part("""{"alg":"HS256"}""")}.{Part("""{"courierId":"courier-007","role":["Courier","Admin"],"exp":1893456000}""")}.sig";

        var info = JwtClaimsReader.TryRead(token)!;

        Assert.Equal("courier-007", info.CourierId);
        Assert.Equal(new[] { "Courier", "Admin" }, info.Roles);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893456000), info.ExpiresAtUtc);
        Assert.Null(JwtClaimsReader.TryRead("not-a-token"));
    }
}
