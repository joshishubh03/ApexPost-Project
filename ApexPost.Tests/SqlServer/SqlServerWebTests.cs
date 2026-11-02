using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Queries;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Tests.SqlServer;

/// <summary>
/// MVC / API tests through WebApplicationFactory against the disposable SQL Server test database.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerWebTests(SqlServerTestDatabase database)
{
    [Fact]
    public async Task Counter_sale_posted_through_mvc_is_persisted_in_sql_server()
    {
        var data = await TestData.LoadAsync(database);
        await using var factory = new SqlServerWebFactory(database.ConnectionString);
        using var client = factory.CreateStaffClient(StaffRole.CounterClerk, data.ClerkId, data.BranchId);
        var stockBefore = await StockAsync(data);
        var correlation = Guid.NewGuid().ToString("D");

        using var response = await PostSaleAsync(client, data, quantity: 1, correlation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(stockBefore - 1, await StockAsync(data));
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        Assert.True(await context.AuditEntries.AnyAsync(row => row.CorrelationId == correlation && row.Action == "CounterTransactionCompleted"));
    }

    [Fact]
    public async Task Over_posting_status_id_and_audit_fields_has_no_effect()
    {
        var data = await TestData.LoadAsync(database);
        await using var factory = new SqlServerWebFactory(database.ConnectionString);
        using var client = factory.CreateStaffClient(StaffRole.CounterClerk, data.ClerkId, data.BranchId);
        var postedId = Guid.NewGuid();
        var correlation = Guid.NewGuid().ToString("D");

        using var response = await PostSaleAsync(client, data, quantity: 1, correlation, extra: new Dictionary<string, string>
        {
            ["Id"] = postedId.ToString(),
            ["Status"] = nameof(TransactionStatus.Voided),
            ["TotalAmount"] = "0.01",
            ["StaffId"] = Guid.NewGuid().ToString(),
            ["CreatedAtUtc"] = "2000-01-01",
            ["CreatedBy"] = Guid.NewGuid().ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        var transactionId = await context.AuditEntries.AsNoTracking()
            .Where(row => row.CorrelationId == correlation && row.Action == "CounterTransactionCompleted")
            .Select(row => row.EntityId)
            .SingleAsync();
        var saved = await context.CounterTransactions.AsNoTracking().SingleAsync(row => row.Id == transactionId);
        Assert.NotEqual(postedId, saved.Id);
        Assert.Equal(TransactionStatus.Paid, saved.Status);
        Assert.Equal(data.ClerkId, saved.StaffId);
        Assert.True(saved.TotalAmount > 0.01m);
        var createdAt = await context.CounterTransactions.AsNoTracking()
            .Where(row => row.Id == saved.Id)
            .Select(row => EF.Property<DateTime>(row, AuditShadowProperties.CreatedAtUtc))
            .SingleAsync();
        Assert.True(createdAt.Year >= 2026);
    }

    [Fact]
    public async Task A_void_requires_the_branch_manager_role()
    {
        var data = await TestData.LoadAsync(database);
        Guid transactionId;
        await using (var scope = database.Services.CreateAsyncScope())
        {
            transactionId = (await scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>()
                .GetRecentForBranchAsync(data.BranchId, 1)).Single().Id;
        }

        await using var factory = new SqlServerWebFactory(database.ConnectionString);
        using var clerk = factory.CreateStaffClient(StaffRole.CounterClerk, data.ClerkId, data.BranchId);
        var token = await GetAntiforgeryTokenAsync(clerk, $"/CounterTransactions?branchId={data.BranchId}");

        using var response = await clerk.PostAsync("/CounterTransactions/Reverse", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["TransactionId"] = transactionId.ToString(),
            ["Reason"] = "Clerk attempting a void"
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Daily_sales_report_returns_the_same_totals_as_the_stored_procedure()
    {
        var data = await TestData.LoadAsync(database);
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));
        DailyBranchSalesDto expected;
        await using (var scope = database.Services.CreateAsyncScope())
        {
            expected = (await scope.ServiceProvider.GetRequiredService<IReportingQueries>()
                .GetDailyBranchSalesAsync(data.BranchCode, day)).Single();
        }

        await using var factory = new SqlServerWebFactory(database.ConnectionString);
        using var client = factory.CreateStaffClient(StaffRole.BranchManager, data.ManagerId, data.BranchId);
        var report = await client.GetFromJsonAsync<List<DailyBranchSalesDto>>(
            $"/api/reports/daily-sales?branchCode={data.BranchCode}&date={day:yyyy-MM-dd}");

        var row = Assert.Single(report!);
        Assert.Equal(expected.TransactionCount, row.TransactionCount);
        Assert.Equal(expected.RetailRevenue, row.RetailRevenue);
        Assert.Equal(expected.ServiceRevenue, row.ServiceRevenue);

        using var page = await client.GetAsync($"/Reports/Operations?BranchCode={data.BranchCode}&Date={day:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("usp_DailyBranchSales", await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Errors_are_returned_as_problem_details()
    {
        var data = await TestData.LoadAsync(database);
        await using var factory = new SqlServerWebFactory(database.ConnectionString);
        using var client = factory.CreateStaffClient(StaffRole.Auditor, data.ManagerId, data.BranchId);

        using var notFound = await client.GetAsync("/api/reports/daily-sales?branchCode=ZZ-404&date=2026-01-01");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("application/problem+json", notFound.Content.Headers.ContentType?.MediaType);
        using var notFoundJson = JsonDocument.Parse(await notFound.Content.ReadAsStringAsync());
        Assert.Equal(404, notFoundJson.RootElement.GetProperty("status").GetInt32());
        Assert.True(notFoundJson.RootElement.TryGetProperty("correlationId", out _));

        using var invalid = await client.GetAsync($"/api/reports/daily-sales?branchCode={data.BranchCode}&date=2999-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Concurrency_conflict_on_stock_returns_409()
    {
        var data = await TestData.LoadAsync(database, productOffset: 4);
        await using (var scope = database.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().BranchStock
                .Where(row => row.BranchId == data.BranchId && row.ProductId == data.ProductId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.QuantityOnHand, 1));
        }

        var interceptor = new BumpStockBeforeSaveInterceptor(database.ConnectionString, data.BranchId, data.ProductId);
        await using var factory = new SqlServerWebFactory(database.ConnectionString, interceptor);
        using var client = factory.CreateStaffClient(StaffRole.CounterClerk, data.ClerkId, data.BranchId);

        using var response = await PostSaleAsync(client, data, quantity: 1, Guid.NewGuid().ToString("D"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("409 Conflict", await response.Content.ReadAsStringAsync());
        Assert.True(interceptor.Fired);
        Assert.True(await StockAsync(data) >= 0);
    }

    private async Task<int> StockAsync(TestData data)
    {
        await using var scope = database.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().BranchStock.AsNoTracking()
            .Where(row => row.BranchId == data.BranchId && row.ProductId == data.ProductId)
            .Select(row => row.QuantityOnHand)
            .SingleAsync();
    }

    private static async Task<HttpResponseMessage> PostSaleAsync(
        HttpClient client,
        TestData data,
        int quantity,
        string correlation,
        IReadOnlyDictionary<string, string>? extra = null)
    {
        var token = await GetAntiforgeryTokenAsync(client, $"/CounterTransactions?branchId={data.BranchId}");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["BranchId"] = data.BranchId.ToString(),
            ["TerminalId"] = data.TerminalId.ToString(),
            ["PaymentMethod"] = nameof(PaymentMethod.Cash),
            [$"Quantities[{data.ProductId}]"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach (var (key, value) in extra ?? new Dictionary<string, string>())
        {
            form[key] = value;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/CounterTransactions/Complete")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Add("X-Correlation-ID", correlation);
        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"{path} should render a form with an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>Simulates another counter selling the same stock row between this request's read and its commit.</summary>
    private sealed class BumpStockBeforeSaveInterceptor(string connectionString, Guid branchId, Guid productId) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var changesStock = eventData.Context?.ChangeTracker.Entries<BranchStockRow>()
                .Any(entry => entry.State == EntityState.Modified) == true;
            if (!Fired && changesStock)
            {
                Fired = true;
                await using var other = new ApexPostDbContext(new DbContextOptionsBuilder<ApexPostDbContext>()
                    .UseSqlServer(connectionString).Options);
                await other.BranchStock
                    .Where(row => row.BranchId == branchId && row.ProductId == productId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.QuantityOnHand, row => row.QuantityOnHand - 1), cancellationToken);
            }

            return result;
        }
    }

    private sealed class SqlServerWebFactory(string connectionString, ISaveChangesInterceptor? extraInterceptor = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Persistence:Provider", "SqlServer");
            builder.UseSetting("ConnectionStrings:ApexPostDb", connectionString);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "SqlServer",
                ["ConnectionStrings:ApexPostDb"] = connectionString,
                ["Persistence:ApplyMigrationsOnStartup"] = "false",
                ["PartnerApi:ApiKey"] = "integration-test-key"
            }));
            builder.ConfigureTestServices(services =>
            {
                if (extraInterceptor is not null)
                {
                    services.AddSingleton(extraInterceptor);
                }

                services.AddAuthentication(options => options.DefaultAuthenticateScheme = StaffHeaderAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, StaffHeaderAuthenticationHandler>(StaffHeaderAuthenticationHandler.SchemeName, _ => { });
            });
        }

        public HttpClient CreateStaffClient(StaffRole role, Guid staffId, Guid branchId)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Add("X-Test-Role", role.ToString());
            client.DefaultRequestHeaders.Add("X-Test-Staff", staffId.ToString());
            client.DefaultRequestHeaders.Add("X-Test-Branch", branchId.ToString());
            return client;
        }
    }

    private sealed class StaffHeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "SqlIntegrationTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Enum.TryParse<StaffRole>(Request.Headers["X-Test-Role"], out var role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-Staff"].ToString()),
                new Claim(ClaimTypes.Name, "SQL integration test user"),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("branch_id", Request.Headers["X-Test-Branch"].ToString())
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
