using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Tests.SqlServer;

/// <summary>Identifiers of seeded rows used by the SQL Server tests (branch AP-001 and its staff).</summary>
public sealed record TestData(
    Guid BranchId,
    string BranchCode,
    Guid ClerkId,
    Guid SecondClerkId,
    Guid ManagerId,
    string ManagerEmail,
    Guid ProductId,
    string ProductSku,
    Guid TerminalId,
    TestRate Rate,
    string ZoneName)
{
    public static async Task<TestData> LoadAsync(SqlServerTestDatabase database, int productOffset = 0)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        var branch = (await context.Branches.AsNoTracking().ToListAsync()).OrderBy(row => row.Code.Value).First();
        var clerks = await context.Staff.AsNoTracking()
            .Where(row => row.BranchId == branch.Id && row.Role == nameof(StaffRole.CounterClerk) && row.IsActive)
            .OrderBy(row => row.Email)
            .ToListAsync();
        var manager = await context.Staff.AsNoTracking()
            .FirstAsync(row => row.BranchId == branch.Id && row.Role == nameof(StaffRole.BranchManager));
        var product = (await context.Products.AsNoTracking().Where(row => row.Category == "Packaging" && row.IsActive).ToListAsync())
            .OrderBy(row => row.Sku.Value)
            .Skip(1 + productOffset)
            .First();
        var terminal = await context.CounterTerminals.AsNoTracking()
            .Where(row => row.BranchId == branch.Id && row.IsActive)
            .OrderBy(row => row.Code)
            .FirstAsync();
        var rate = await (
                from shippingRate in context.ShippingRates.AsNoTracking()
                join zone in context.ShippingZones.AsNoTracking() on shippingRate.ZoneId equals zone.Id
                where shippingRate.IsActive && zone.IsActive && shippingRate.ServiceLevel == ShippingServiceLevel.Standard
                orderby zone.Code
                select new { shippingRate.Id, shippingRate.ServiceLevel, ZoneName = zone.Name })
            .FirstAsync();

        return new TestData(
            branch.Id,
            branch.Code.Value,
            clerks[0].Id,
            clerks[1].Id,
            manager.Id,
            manager.Email,
            product.Id,
            product.Sku.Value,
            terminal.Id,
            new TestRate(rate.Id, rate.ServiceLevel),
            rate.ZoneName);
    }
}

public sealed record TestRate(Guid Id, ShippingServiceLevel ServiceLevel);
