using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Infrastructure.Data;

public sealed class ApexPostDbContextFactory : IDesignTimeDbContextFactory<ApexPostDbContext>
{
    public ApexPostDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ApexPostDb")
            ?? Environment.GetEnvironmentVariable("APEXPOST_DB_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApexPostDbContext(options);
    }
}
