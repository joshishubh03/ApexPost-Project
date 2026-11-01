using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApexPost.Infrastructure.Persistence;

public sealed class ApexPostDbContextFactory : IDesignTimeDbContextFactory<ApexPostDbContext>
{
    public ApexPostDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ApexPost") ??
            Environment.GetEnvironmentVariable("APEXPOST_SQLSERVER") ??
            "Server=(localdb)\\MSSQLLocalDB;Database=ApexPostCounter;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ApexPostDbContext).Assembly.FullName))
            .Options;

        return new ApexPostDbContext(options);
    }
}
