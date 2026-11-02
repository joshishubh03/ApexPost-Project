using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Tests.SqlServer;

/// <summary>
/// Disposable SQL Server database for integration tests (never the development database).
/// Created with a unique name, migrated with the real migrations (which also create the stored procedures),
/// seeded through the real DemoDataSeeder, and dropped when the test class finishes.
/// Server: APEXPOST_TEST_SQLSERVER environment variable, otherwise (localdb)\MSSQLLocalDB.
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    public const string DemoPassword = "Test-Only-Password-1!";

    public SqlServerTestDatabase()
    {
        var server = Environment.GetEnvironmentVariable("APEXPOST_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server)
            ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
            : server)
        {
            InitialCatalog = $"ApexPostDb_Test_{Guid.NewGuid():N}",
            MultipleActiveResultSets = true
        };
        ConnectionString = builder.ConnectionString;
        Services = CreateServices(ConnectionString);
    }

    public string ConnectionString { get; }
    public ServiceProvider Services { get; }

    public static MoneyOrderPolicy MoneyOrderPolicy { get; } = new(10_000m, 50m, "DEMO-VALID-");

    public static ServiceProvider CreateServices(string connectionString, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApexPostInfrastructure(
            Path.Combine(Path.GetTempPath(), "apexpost-sql-tests-unused"),
            DemoPassword,
            MoneyOrderPolicy,
            enableSimulatedIdentityVerification: true,
            connectionString,
            useSqlServer: true);
        services.AddApexPostSqlServer(connectionString, 60);
        services.AddSingleton(MoneyOrderPolicy);
        services.AddSingleton(new TaxPolicy(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Standard"] = 0m,
            [TaxPolicy.ShippingCategory] = 0m
        }));
        services.AddSingleton(LowStockRule.AtOrBelowReorderLevel);
        services.AddScoped<InventoryManagementService>();
        services.AddScoped<ShippingRateService>();
        services.AddScoped<MoneyOrderService>();
        services.AddScoped<CounterTransactionService>();
        services.AddScoped<ProductCatalogueService>();
        services.AddScoped<ReportingService>();
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    public async Task InitializeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
            await context.Database.MigrateAsync();
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            // Test-only teardown of the disposable database (application code never calls EnsureCreated/EnsureDeleted).
            await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.EnsureDeletedAsync();
        }

        await Services.DisposeAsync();
        SqlConnection.ClearAllPools();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerTestDatabase>
{
    public const string Name = "SQL Server integration database";
}
