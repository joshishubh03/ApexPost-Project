using System.Net.Http.Headers;
using System.Security.Cryptography;
using ApexPost.Api;
using ApexPost.Api.Security;
using ApexPost.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Specs.Support;

/// <summary>
/// ApexPost.Api in memory, on its own disposable SQL Server database (real migrations, dropped at the end of the
/// run), with a test-only JWT signing key. Server: APEXPOST_TEST_SQLSERVER, otherwise (localdb)\MSSQLLocalDB.
/// One host per test run (started and stopped by <see cref="TestRunHooks"/>), like an xUnit collection fixture.
/// </summary>
public sealed class ShipmentApiHost : WebApplicationFactory<ApiAssemblyMarker>
{
    private const string Issuer = "https://identity.apexpost.example";
    private const string Audience = "apexpost-api";

    // Random per run and never stored: this is a test key, not a secret.
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public ShipmentApiHost()
    {
        var server = Environment.GetEnvironmentVariable("APEXPOST_TEST_SQLSERVER");
        ConnectionString = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server)
            ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
            : server)
        {
            InitialCatalog = $"ApexPostDb_Specs_{Guid.NewGuid():N}",
            MultipleActiveResultSets = true
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    /// <summary>An HTTPS client carrying a test token with the given roles.</summary>
    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var token = new DevelopmentTokenIssuer(
                new JwtSettings { Issuer = Issuer, Audience = Audience, SigningKey = SigningKey },
                TimeProvider.System)
            .Issue(roles, courierId: "courier-007").AccessToken;

        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task MigrateAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.MigrateAsync();
    }

    /// <summary>Removes the shipments (and their scans) a scenario created, so scenarios never see each other's data.</summary>
    public async Task DeleteShipmentsAsync(IReadOnlyCollection<string> trackingNumbers)
    {
        if (trackingNumbers.Count == 0)
        {
            return;
        }

        var numbers = trackingNumbers.ToList();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        var ids = await db.CourierShipments
            .Where(shipment => numbers.Contains(shipment.TrackingNumber))
            .Select(shipment => shipment.Id)
            .ToListAsync();
        await db.ShipmentScans.Where(scan => ids.Contains(scan.CourierShipmentId)).ExecuteDeleteAsync();
        await db.CourierShipments.Where(shipment => ids.Contains(shipment.Id)).ExecuteDeleteAsync();
    }

    public async Task DropDatabaseAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.EnsureDeletedAsync();
        }

        SqlConnection.ClearAllPools();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ApexPostDb", ConnectionString);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("PartnerApi:Keys:demo-partner", "specs-partner-key-not-a-secret");
    }
}
