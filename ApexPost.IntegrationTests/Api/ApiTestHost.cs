using System.Net.Http.Headers;
using ApexPost.Api;
using ApexPost.Api.Security;
using ApexPost.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Api.Tests;

/// <summary>
/// ApexPost.Api hosted in memory against a disposable SQL Server database (migrated with the real migrations,
/// dropped afterwards). Server: APEXPOST_TEST_SQLSERVER, otherwise (localdb)\MSSQLLocalDB.
/// </summary>
public sealed class ApiTestHost : WebApplicationFactory<ApiAssemblyMarker>, IAsyncLifetime
{
    private static int trackingSeed = Random.Shared.Next(10_000_000, 80_000_000);

    public ApiTestHost()
    {
        var server = Environment.GetEnvironmentVariable("APEXPOST_TEST_SQLSERVER");
        ConnectionString = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server)
            ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
            : server)
        {
            InitialCatalog = $"ApexPostDb_ApiTest_{Guid.NewGuid():N}",
            MultipleActiveResultSets = true
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    /// <summary>Test-only signing key (random per run, never a real secret).</summary>
    public static readonly string SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    public const string Issuer = "https://identity.apexpost.example";
    public const string Audience = "apexpost-api";
    public const string PartnerKey = "test-partner-key-not-a-secret";

    public static JwtSettings JwtSettings { get; } = new() { Issuer = Issuer, Audience = Audience, SigningKey = SigningKey };

    /// <summary>Mints a token signed with the test key.</summary>
    public static string Token(string roles, string? courierId = "courier-007", bool expired = false) =>
        new DevelopmentTokenIssuer(JwtSettings, TimeProvider.System)
            .Issue(roles.Split(','), courierId, expired: expired).AccessToken;

    /// <summary>HTTPS client with a token holding both roles (used by the functional tests).</summary>
    public HttpClient CreateAuthenticatedClient(string roles = "Admin,Courier", string? courierId = "courier-007")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(roles, courierId));
        return client;
    }

    /// <summary>HTTPS client without credentials.</summary>
    public HttpClient CreateAnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>Settings every test host needs (the signing key normally comes from User Secrets).</summary>
    public static void ApplySecuritySettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("PartnerApi:Keys:demo-partner", PartnerKey);
    }

    /// <summary>A tracking number no other test uses.</summary>
    public static string NewTrackingNumber() => $"APX-{Interlocked.Increment(ref trackingSeed):D8}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ApexPostDb", ConnectionString);
        ApplySecuritySettings(builder);
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.EnsureDeletedAsync();
        }

        SqlConnection.ClearAllPools();
        await DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiTestHost>
{
    public const string Name = "ApexPost.Api on a disposable SQL Server database";
}
