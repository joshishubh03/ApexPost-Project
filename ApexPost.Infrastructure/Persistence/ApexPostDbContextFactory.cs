using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>Creates the same SQL Server context/database used by the Web application at design time.</summary>
public sealed class ApexPostDbContextFactory : IDesignTimeDbContextFactory<ApexPostDbContext>
{
    public ApexPostDbContext CreateDbContext(string[] args)
    {
        var webProjectDirectory = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "ApexPost.Web"))
            ? Path.Combine(Directory.GetCurrentDirectory(), "ApexPost.Web")
            : Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
     .SetBasePath(webProjectDirectory)
     .AddJsonFile("appsettings.json", optional: true)
     .AddJsonFile("appsettings.Development.json", optional: true)
     .AddUserSecrets(Assembly.Load("ApexPost.Web"), optional: true)
     .AddEnvironmentVariables()
     .Build();

        var connectionString = configuration.GetConnectionString("ApexPostDb")
            ?? Environment.GetEnvironmentVariable("APEXPOST_DB_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(ApexPostDbContext).Assembly.GetName().Name))
            .Options;

        return new ApexPostDbContext(options);
    }
}
