using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ApexPost.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.Api.Tests;

/// <summary>
/// Version-specific OpenAPI documents (Development only) and contract tracking: /contracts/v1.json and v2.json must
/// match what the API generates. After an intentional API change run scripts/update-contracts.ps1
/// (APEXPOST_UPDATE_CONTRACTS=1), review the diff, update VERSIONING.md and commit.
/// </summary>
public sealed class OpenApiDocumentTests : IClassFixture<OpenApiDocumentTests.DevelopmentHost>
{
    private readonly HttpClient client;

    public OpenApiDocumentTests(DevelopmentHost host) =>
        client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Committed_contract_matches_the_generated_document(string version)
    {
        var generated = await client.GetStringAsync($"/openapi/{version}.json");
        var path = Path.Combine(RepositoryRoot(), "contracts", $"{version}.json");

        // The first committed contracts were hand-written seeds ("x-apexpost-seed": true). The first test run
        // replaces a seed with the real generated document; from then on any drift fails this test.
        var isSeed = File.Exists(path) &&
                     JsonNode.Parse(await File.ReadAllTextAsync(path))?["x-apexpost-seed"]?.GetValue<bool>() == true;
        if (isSeed || Environment.GetEnvironmentVariable("APEXPOST_UPDATE_CONTRACTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, Pretty(generated) + Environment.NewLine);
            return;
        }

        Assert.True(File.Exists(path), $"Missing {path}.");
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(generated), JsonNode.Parse(await File.ReadAllTextAsync(path))),
            $"contracts/{version}.json is out of date with the API. Run scripts/update-contracts.ps1, review the diff and record the change in VERSIONING.md.");
    }

    [Fact]
    public async Task Documents_carry_the_title_contact_and_versions()
    {
        foreach (var version in new[] { "v1", "v2" })
        {
            var document = await GetDocumentAsync(version);
            var info = document.GetProperty("info");
            Assert.Equal("ApexPost Logistics API", info.GetProperty("title").GetString());
            Assert.Equal(version, info.GetProperty("version").GetString());
            Assert.EndsWith("@apexpost.example", info.GetProperty("contact").GetProperty("email").GetString(), StringComparison.Ordinal);
            Assert.StartsWith("3.0", document.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task V1_operations_are_deprecated_and_v2_operations_are_not()
    {
        var v1 = Operations(await GetDocumentAsync("v1")).ToArray();
        var v2 = Operations(await GetDocumentAsync("v2")).ToArray();

        Assert.NotEmpty(v1);
        Assert.All(v1, operation => Assert.True(operation.TryGetProperty("deprecated", out var flag) && flag.GetBoolean()));
        Assert.All(v2, operation => Assert.False(operation.TryGetProperty("deprecated", out var flag) && flag.GetBoolean()));
        Assert.Contains(v1, operation => operation.GetProperty("operationId").GetString() == "ListShipmentsV1");
    }

    [Fact]
    public async Task V2_has_stable_operation_ids_tags_and_documented_status_codes()
    {
        var document = await GetDocumentAsync("v2");
        var operationIds = Operations(document).Select(operation => operation.GetProperty("operationId").GetString()).ToHashSet();
        foreach (var id in new[] { "ListShipments", "GetShipment", "CreateShipment", "ReplaceShipment", "UpdateShipmentStatus", "DeleteShipment", "UploadScans", "GetScanHistory" })
        {
            Assert.Contains(id, operationIds);
        }

        var create = document.GetProperty("paths").GetProperty("/api/v2/shipments").GetProperty("post");
        var responses = create.GetProperty("responses");
        foreach (var code in new[] { "201", "400", "409", "415", "500" })
        {
            Assert.True(responses.TryGetProperty(code, out _), $"CreateShipment should document {code}.");
        }

        Assert.Equal("Shipments", create.GetProperty("tags")[0].GetString());
        Assert.False(string.IsNullOrWhiteSpace(create.GetProperty("summary").GetString()));
    }

    [Fact]
    public async Task Documents_declare_the_bearer_and_partner_api_key_schemes()
    {
        var document = await GetDocumentAsync("v2");
        var schemes = document.GetProperty("components").GetProperty("securitySchemes");

        Assert.Equal("bearer", schemes.GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.Equal("X-Api-Key", schemes.GetProperty("PartnerApiKey").GetProperty("name").GetString());
        var partner = document.GetProperty("paths").GetProperty("/api/v2/partner/shipments/{trackingNumber}").GetProperty("get");
        Assert.True(partner.GetProperty("security")[0].TryGetProperty("PartnerApiKey", out _));
        var create = document.GetProperty("paths").GetProperty("/api/v2/shipments").GetProperty("post");
        Assert.True(create.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(create.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(create.GetProperty("responses").TryGetProperty("403", out _));
    }

    [Fact]
    public async Task Development_token_endpoint_issues_tokens_that_the_api_accepts()
    {
        var token = JsonDocument.Parse(await client.GetStringAsync("/dev/token?roles=Courier&courierId=courier-042"))
            .RootElement.GetProperty("accessToken").GetString();

        // A Courier calling an Admin-only endpoint: 403 (authenticated, wrong role) - not 401 - proves the issued
        // token passed issuer, audience, lifetime and signature validation. No database is needed for this check.
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v2/shipments/APX-12345678");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ui_is_served_in_development()
    {
        using var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<JsonElement> GetDocumentAsync(string version) =>
        JsonDocument.Parse(await client.GetStringAsync($"/openapi/{version}.json")).RootElement.Clone();

    private static IEnumerable<JsonElement> Operations(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(method => method.Name is "get" or "post" or "put" or "patch" or "delete")
            .Select(method => method.Value);

    private static string Pretty(string json) =>
        JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ApexPost.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root (ApexPost.slnx).");
    }

    /// <summary>The API in the Development environment (documents and Swagger UI on). No database is touched.</summary>
    public sealed class DevelopmentHost : WebApplicationFactory<ApiAssemblyMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ApexPostDb",
                @"Server=(localdb)\MSSQLLocalDB;Database=ApexPost_OpenApi_NotOpened;Trusted_Connection=True;TrustServerCertificate=True");
            builder.UseSetting("Persistence:ApplyMigrationsOnStartup", "false");
            ApiTestHost.ApplySecuritySettings(builder);
        }
    }
}
