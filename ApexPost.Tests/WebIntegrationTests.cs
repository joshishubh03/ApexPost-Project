using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Tests.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApexPost.Tests;

/// <summary>
/// Full HTTP pipeline tests (security headers, rate limiting, IP allow-lists, authorization, audit filter,
/// Problem Details) through WebApplicationFactory against the disposable SQL Server test database - the same
/// persistence the application uses in every environment.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class WebIntegrationTests(SqlServerTestDatabase database)
{
    [Fact]
    public async Task Security_headers_are_added_to_mvc_responses()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory);

        using var response = await client.GetAsync("/Account/Login");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Partner_endpoint_requires_the_configured_api_key()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory);

        using var missingKeyResponse = await client.GetAsync("/api/partner/tracking/unknown");
        Assert.Equal(HttpStatusCode.Unauthorized, missingKeyResponse.StatusCode);

        client.DefaultRequestHeaders.Add("X-API-Key", "integration-test-key");
        using var validKeyResponse = await client.GetAsync("/api/partner/tracking/unknown");
        Assert.Equal(HttpStatusCode.NotFound, validKeyResponse.StatusCode);
    }

    [Fact]
    public async Task Partner_endpoint_is_forbidden_when_remote_ip_is_not_allowlisted()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString, integrationAllowedIpAddress: "192.0.2.10");
        using var client = CreateSecureClient(factory);
        client.DefaultRequestHeaders.Add("X-API-Key", "integration-test-key");

        using var response = await client.GetAsync("/api/partner/tracking/unknown");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tracking_widget_origin_can_make_a_cors_preflight_request()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/partner/tracking/unknown");
        request.Headers.Add("Origin", "https://widget.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "X-API-Key");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://widget.example", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Administration_path_is_denied_when_remote_ip_is_not_allowlisted()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory);

        using var response = await client.GetAsync("/Administration/Branches");
        using var productMutation = await client.PostAsync("/Catalog/Create", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, productMutation.StatusCode);
    }

    [Fact]
    public async Task Trusted_proxy_forwarded_for_is_used_for_admin_ip_allowlist()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString, trustLoopbackProxy: true);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        Assert.Equal("Testing", factory.Services.GetRequiredService<IWebHostEnvironment>().EnvironmentName);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.Administrator));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Administration/Branches");
        request.Headers.Add("X-Forwarded-For", "192.0.2.10");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Untrusted_forwarded_for_cannot_bypass_admin_ip_allowlist()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.Administrator));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Administration/Branches");
        request.Headers.Add("X-Forwarded-For", "192.0.2.10");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task Login_post_is_rate_limited_after_five_attempts()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "unknown@apexpost.local",
                ["Password"] = "wrong-password",
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = antiforgeryToken
            }));
        }

        using var response = lastResponse ?? throw new InvalidOperationException("The login attempts did not return a response.");
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_staff_page_redirects_to_login()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);

        using var response = await client.GetAsync("/Catalog/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Staff_cannot_request_inventory_for_another_branch()
    {
        var data = await TestData.LoadAsync(database);
        var otherBranchId = await OtherBranchIdAsync(data.BranchId);
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false, data: data);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.CounterClerk));

        using var response = await client.GetAsync($"/Inventory/Index?branchId={otherBranchId:D}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_open_the_cross_branch_overview()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.Auditor));

        using var response = await client.GetAsync("/Reports/AuditOverview");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Auditor cross-branch overview", html);
    }

    [Fact]
    public async Task Mvc_audit_filter_records_safe_product_changes()
    {
        var data = await TestData.LoadAsync(database);
        var sku = $"AUD-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var correlationId = Guid.NewGuid().ToString("D");
        using var factory = new ApexPostWebFactory(database.ConnectionString, trustLoopbackProxy: true);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false, data: data);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.Administrator));
        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, "/Catalog/Index");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/Catalog/Create");
        createRequest.Headers.Add("X-Forwarded-For", "192.0.2.10");
        createRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NewProduct.Sku"] = sku,
            ["NewProduct.Name"] = "Audit Sample Product",
            ["NewProduct.Description"] = "Created by the MVC audit integration test.",
            ["NewProduct.Category"] = "Stationery",
            ["NewProduct.TaxCategory"] = "Standard",
            ["NewProduct.Price"] = "8.95",
            ["__RequestVerificationToken"] = antiforgeryToken
        });
        using var createResponse = await client.SendAsync(createRequest);
        Assert.True(createResponse.StatusCode == HttpStatusCode.Redirect,
            $"Product creation returned {(int)createResponse.StatusCode}: {await createResponse.Content.ReadAsStringAsync()}");

        Product product;
        await using (var scope = database.Services.CreateAsyncScope())
        {
            var products = await scope.ServiceProvider.GetRequiredService<IProductRepository>().GetAllAsync();
            product = Assert.Single(products, item => item.Sku == sku);
        }

        using var deactivateRequest = new HttpRequestMessage(HttpMethod.Post, "/Catalog/SetActive");
        deactivateRequest.Headers.Add("X-Forwarded-For", "192.0.2.10");
        deactivateRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = product.Id.ToString("D"),
            ["isActive"] = "false",
            ["__RequestVerificationToken"] = antiforgeryToken
        });
        using var deactivateResponse = await client.SendAsync(deactivateRequest);
        Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);

        IReadOnlyList<AuditRecord> records;
        await using (var scope = database.Services.CreateAsyncScope())
        {
            records = (await scope.ServiceProvider.GetRequiredService<IAuditLogRepository>().GetAllAsync())
                .Where(record => record.CorrelationId == correlationId && record.EntityType == "Catalog")
                .ToArray();
        }

        Assert.Equal(2, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(data.BranchId, record.BranchId);
            Assert.Equal(nameof(StaffRole.Administrator), record.Role);
            Assert.Equal(StatusCodes.Status302Found, record.ResultStatusCode);
        });
        Assert.Contains(records, record => record.Action == "MvcCatalogCreate");
        Assert.Contains(records, record => record.Action == "MvcCatalogSetActive" &&
            record.RouteValues?.GetValueOrDefault("id") == product.Id.ToString("D"));

        var samplePath = Environment.GetEnvironmentVariable("APEXPOST_AUDIT_SAMPLE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(samplePath))
        {
            var fullPath = Path.GetFullPath(samplePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(fullPath, json);
        }
    }

    [Fact]
    public async Task Counter_clerk_gets_forbidden_when_posting_a_transaction_reversal()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.CounterClerk));

        using var response = await client.PostAsync("/CounterTransactions/Reverse", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TransactionId"] = Guid.NewGuid().ToString("D"),
            ["Reason"] = "Customer request",
            ["IsRefund"] = "true"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Shipping_label_is_rendered_only_for_the_staff_members_branch()
    {
        var data = await TestData.LoadAsync(database);
        var otherBranchId = await OtherBranchIdAsync(data.BranchId);
        var ownShipment = await FindShipmentAsync(data.BranchId);
        var otherShipment = await FindShipmentInAnotherBranchAsync(data.BranchId);
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false, data: data);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.CounterClerk));

        // A clerk's own branch always wins over a branchId in the query string.
        using var authorizedLabel = await client.GetAsync($"/CounterTransactions/Label?id={ownShipment.Id:D}&branchId={otherBranchId:D}");
        var authorizedHtml = await authorizedLabel.Content.ReadAsStringAsync();
        using var crossBranchLabel = await client.GetAsync($"/CounterTransactions/Label?id={otherShipment.Id:D}");

        Assert.Equal(HttpStatusCode.OK, authorizedLabel.StatusCode);
        Assert.Contains(HtmlEncoder.Default.Encode(ownShipment.ShippingRecipientName!), authorizedHtml);
        Assert.Equal(HttpStatusCode.NotFound, crossBranchLabel.StatusCode);
    }

    [Fact]
    public async Task Web_host_runs_on_the_sql_server_test_database()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory);
        using var health = await client.GetAsync("/health");

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.True(context.Database.IsSqlServer());
        Assert.Equal(new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog, context.Database.GetDbConnection().Database);
        Assert.IsType<EfUnitOfWork>(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.True(await context.Branches.AnyAsync());
    }

    [Fact]
    public async Task Correlation_id_is_returned_to_the_caller()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString);
        using var client = CreateSecureClient(factory);
        var correlationId = Guid.NewGuid().ToString("D");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await client.SendAsync(request);

        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Maintenance_mode_keeps_health_available_and_returns_503_elsewhere()
    {
        using var factory = new ApexPostWebFactory(database.ConnectionString, maintenanceMode: true);
        using var client = CreateSecureClient(factory);

        using var healthResponse = await client.GetAsync("/health");
        using var loginResponse = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, loginResponse.StatusCode);
        Assert.Equal("300", loginResponse.Headers.GetValues("Retry-After").Single());
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path = "/Account/Login")
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "The login form should contain an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static HttpClient CreateSecureClient(ApexPostWebFactory factory, bool allowAutoRedirect = true, TestData? data = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = allowAutoRedirect
        });
        if (data is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Staff", data.ClerkId.ToString("D"));
            client.DefaultRequestHeaders.Add("X-Test-Branch", data.BranchId.ToString("D"));
        }

        return client;
    }

    private async Task<Guid> OtherBranchIdAsync(Guid branchId)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        return await context.Branches.AsNoTracking()
            .Where(branch => branch.Id != branchId)
            .OrderBy(branch => branch.Id)
            .Select(branch => branch.Id)
            .FirstAsync();
    }

    // Only shipments created by tests carry a recipient name, and tests create them in the staff member's own
    // branch. The seeded shipments in other branches have a shipping rate but no recipient. That is enough here:
    // the request for the other branch must return 404 whatever the label would contain.
    private async Task<CounterTransaction> FindShipmentInAnotherBranchAsync(Guid ownBranchId)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>();
        var otherBranchIds = await context.Branches.AsNoTracking()
            .Where(branch => branch.Id != ownBranchId)
            .OrderBy(branch => branch.Id)
            .Select(branch => branch.Id)
            .ToListAsync();

        foreach (var branchId in otherBranchIds)
        {
            var shipment = (await repository.GetRecentForBranchAsync(branchId, 500))
                .FirstOrDefault(transaction => transaction.ShippingRateId is not null);
            if (shipment is not null)
            {
                return shipment;
            }
        }

        throw new InvalidOperationException("The test data has no shipment in any branch other than the staff member's.");
    }

    private async Task<CounterTransaction> FindShipmentAsync(Guid branchId)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var transactions = await scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>()
            .GetRecentForBranchAsync(branchId, 500);
        return transactions.First(transaction =>
            transaction.ShippingRateId is not null && !string.IsNullOrWhiteSpace(transaction.ShippingRecipientName));
    }

    private sealed class ApexPostWebFactory(
        string connectionString,
        bool maintenanceMode = false,
        string? integrationAllowedIpAddress = null,
        bool trustLoopbackProxy = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:ApexPostDb", connectionString);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var testSettings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ApexPostDb"] = connectionString,
                    ["Persistence:ApplyMigrationsOnStartup"] = "false",
                    ["PartnerApi:ApiKey"] = "integration-test-key",
                    ["PartnerApi:AllowedCorsOrigins:0"] = "https://widget.example",
                    ["Security:MaintenanceMode"] = maintenanceMode.ToString(),
                    ["Security:AdminAllowedIpAddresses:0"] = "192.0.2.10"
                };
                if (integrationAllowedIpAddress is not null)
                {
                    testSettings["Security:IntegrationAllowedIpAddresses:0"] = integrationAllowedIpAddress;
                    testSettings["Security:IntegrationAllowedIpAddresses:1"] = "192.0.2.11";
                }
                configuration.AddInMemoryCollection(testSettings);
            });
            builder.ConfigureTestServices(services =>
            {
                if (trustLoopbackProxy)
                {
                    services.PostConfigure<ForwardedHeadersOptions>(options =>
                    {
                        options.KnownIPNetworks.Clear();
                        options.KnownProxies.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                        options.KnownProxies.Add(IPAddress.IPv6Loopback);
                    });
                }

                services.AddSingleton<IStartupFilter, LoopbackAddressStartupFilter>();
                services.RemoveAll<ApexPost.Application.Abstractions.IAuthenticationService>();
                services.AddSingleton<ApexPost.Application.Abstractions.IAuthenticationService, TestAuthenticationService>();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddAuthentication(options =>
                    options.DefaultAuthenticateScheme = "IntegrationTest")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("IntegrationTest", _ => { });
            });
        }
    }

    private sealed class LoopbackAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
                await nextMiddleware();
            });
            next(app);
        };
    }

    private sealed class TestAuthenticationService : ApexPost.Application.Abstractions.IAuthenticationService
    {
        public Task<StaffSession?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (password != "integration-password")
            {
                return Task.FromResult<StaffSession?>(null);
            }

            return Task.FromResult<StaffSession?>(new StaffSession(
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                email,
                "Integration Clerk",
                StaffRole.CounterClerk,
                Guid.Parse("20000000-0000-0000-0000-000000000001"),
                "AP-001",
                "Integration Branch"));
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Enum.TryParse<StaffRole>(Request.Headers["X-Test-Role"], out var role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, HeaderGuid("X-Test-Staff", "10000000-0000-0000-0000-000000000001")),
                new Claim(ClaimTypes.Name, "Integration Test User"),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("branch_id", HeaderGuid("X-Test-Branch", "20000000-0000-0000-0000-000000000001"))
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        private string HeaderGuid(string header, string fallback) =>
            Guid.TryParse(Request.Headers[header], out var value) ? value.ToString("D") : fallback;
    }
}
