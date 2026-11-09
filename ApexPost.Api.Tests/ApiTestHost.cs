using ApexPost.Api;
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

    /// <summary>A tracking number no other test uses.</summary>
    public static string NewTrackingNumber() => $"APX-{Interlocked.Increment(ref trackingSeed):D8}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ApexPostDb", ConnectionString);
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
