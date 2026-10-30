using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
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

namespace ApexPost.Tests;

public sealed class WebIntegrationTests
{
    [Fact]
    public async Task Security_headers_are_added_to_mvc_responses()
    {
        using var factory = new ApexPostWebFactory();
        using var client = CreateSecureClient(factory);

        using var response = await client.GetAsync("/Account/Login");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Partner_endpoint_requires_the_configured_api_key()
    {
        using var factory = new ApexPostWebFactory();
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
        using var factory = new ApexPostWebFactory(integrationAllowedIpAddress: "192.0.2.10");
        using var client = CreateSecureClient(factory);
        client.DefaultRequestHeaders.Add("X-API-Key", "integration-test-key");

        using var response = await client.GetAsync("/api/partner/tracking/unknown");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tracking_widget_origin_can_make_a_cors_preflight_request()
    {
        using var factory = new ApexPostWebFactory();
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
        using var factory = new ApexPostWebFactory();
        using var client = CreateSecureClient(factory);

        using var response = await client.GetAsync("/Administration/Branches");
        using var productMutation = await client.PostAsync("/Catalog/Create", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, productMutation.StatusCode);
    }

    [Fact]
    public async Task Trusted_proxy_forwarded_for_is_used_for_admin_ip_allowlist()
    {
        using var factory = new ApexPostWebFactory(trustLoopbackProxy: true);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        Assert.Equal("Testing", factory.Services.GetRequiredService<IWebHostEnvironment>().EnvironmentName);
        Assert.Equal(factory.DataDirectory,
            factory.Services.GetRequiredService<IConfiguration>()["ApexPost:DataDirectory"]);
        Assert.Empty(await factory.Services.GetRequiredService<IProductRepository>().GetAllAsync());
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.Administrator));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Administration/Branches");
        request.Headers.Add("X-Forwarded-For", "192.0.2.10");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Untrusted_forwarded_for_cannot_bypass_admin_ip_allowlist()
    {
        using var factory = new ApexPostWebFactory();
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
        using var factory = new ApexPostWebFactory();
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
        using var factory = new ApexPostWebFactory();
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);

        using var response = await client.GetAsync("/Catalog/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Staff_cannot_request_inventory_for_another_branch()
    {
        using var factory = new ApexPostWebFactory();
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.CounterClerk));

        using var response = await client.GetAsync(
            "/Inventory/Index?branchId=20000000-0000-0000-0000-000000000002");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_open_the_cross_branch_overview()
    {
        using var factory = new ApexPostWebFactory();
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
        using var factory = new ApexPostWebFactory(trustLoopbackProxy: true);
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.Administrator));
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, "/Catalog/Index");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/Catalog/Create");
        createRequest.Headers.Add("X-Forwarded-For", "192.0.2.10");
        createRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NewProduct.Sku"] = "AUDIT-SAMPLE-1",
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

        var products = await factory.Services.GetRequiredService<IProductRepository>().GetAllAsync();
        var product = Assert.Single(products, item => item.Sku == "AUDIT-SAMPLE-1");

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

        var records = await factory.Services.GetRequiredService<IAuditLogRepository>().GetAllAsync();
        Assert.Equal(2, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal("Catalog", record.EntityType);
            Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000001"), record.BranchId);
            Assert.Equal(nameof(StaffRole.Administrator), record.Role);
            Assert.Equal(StatusCodes.Status302Found, record.ResultStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(record.CorrelationId));
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
        using var factory = new ApexPostWebFactory();
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
        using var factory = new ApexPostWebFactory();
        using var client = CreateSecureClient(factory, allowAutoRedirect: false);
        client.DefaultRequestHeaders.Add("X-Test-Role", nameof(StaffRole.CounterClerk));

        using var authorizedLabel = await client.GetAsync("/CounterTransactions/Label?id=30000000-0000-0000-0000-000000000001&branchId=20000000-0000-0000-0000-000000000002");
        var authorizedHtml = await authorizedLabel.Content.ReadAsStringAsync();
        using var crossBranchLabel = await client.GetAsync("/CounterTransactions/Label?id=30000000-0000-0000-0000-000000000002");

        Assert.Equal(HttpStatusCode.OK, authorizedLabel.StatusCode);
        Assert.Contains("Test Recipient", authorizedHtml);
        Assert.Equal(HttpStatusCode.NotFound, crossBranchLabel.StatusCode);
    }

    [Fact]
    public async Task Correlation_id_is_returned_to_the_caller()
    {
        using var factory = new ApexPostWebFactory();
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
        using var factory = new ApexPostWebFactory(maintenanceMode: true);
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

    private static HttpClient CreateSecureClient(ApexPostWebFactory factory, bool allowAutoRedirect = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = allowAutoRedirect
        });

    private sealed class ApexPostWebFactory(
        bool maintenanceMode = false,
        string? integrationAllowedIpAddress = null,
        bool trustLoopbackProxy = false) : WebApplicationFactory<Program>
    {
        private readonly string dataDirectory = Path.Combine(Path.GetTempPath(), $"apexpost-web-tests-{Guid.NewGuid():N}");
        public string DataDirectory => dataDirectory;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ApexPost:DataDirectory", dataDirectory);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var testSettings = new Dictionary<string, string?>
                {
                    ["ApexPost:DataDirectory"] = dataDirectory,
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
                services.RemoveAll<ICounterTransactionRepository>();
                services.AddSingleton<ICounterTransactionRepository, FakeCounterTransactionRepository>();
                services.AddAuthentication(options =>
                    options.DefaultAuthenticateScheme = "IntegrationTest")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("IntegrationTest", _ => { });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
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
                new Claim(ClaimTypes.NameIdentifier, "10000000-0000-0000-0000-000000000001"),
                new Claim(ClaimTypes.Name, "Integration Test User"),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("branch_id", "20000000-0000-0000-0000-000000000001")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class FakeCounterTransactionRepository : ICounterTransactionRepository
    {
        private static readonly Guid BranchId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        private readonly IReadOnlyList<CounterTransaction> transactions =
        [
            CreateShipment(Guid.Parse("30000000-0000-0000-0000-000000000001"), BranchId, "APT-20261025-LABEL001"),
            CreateShipment(Guid.Parse("30000000-0000-0000-0000-000000000002"), Guid.Parse("20000000-0000-0000-0000-000000000002"), "APT-20261025-LABEL002")
        ];

        public Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(transactions);

        public Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private static CounterTransaction CreateShipment(Guid id, Guid branchId, string number) => new(
            id,
            number,
            branchId,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            DateTimeOffset.UtcNow,
            [],
            Guid.NewGuid(),
            "Local Metro",
            ShippingServiceLevel.Standard,
            1m,
            25m,
            PaymentMethod.Cash,
            shippingRecipientName: "Test Recipient",
            shippingAddress: "1 Main Street, Central",
            parcelLengthCentimeters: 20m,
            parcelWidthCentimeters: 15m,
            parcelHeightCentimeters: 10m);
    }
}
