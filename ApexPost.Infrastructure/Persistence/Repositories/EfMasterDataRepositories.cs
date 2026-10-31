using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Repositories;

internal static class PersistenceMapping
{
    public static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException($"The stored value '{value}' is not a valid {typeof(TEnum).Name}.");

    public static AuditColumns NewAudit() => new() { CreatedAtUtc = DateTime.UtcNow };

    public static void Touch(AuditColumns audit)
    {
        audit.UpdatedAtUtc = DateTime.UtcNow;
    }
}

public sealed class EfBranchRepository(ApexPostDbContext context) : IBranchRepository
{
    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.Branches.AsNoTracking().OrderBy(row => row.Code).ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await context.Branches.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default)
    {
        var values = branches.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.Branches.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var value in values)
        {
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (row.Code == value.Code && row.Name == value.Name && row.Region == value.Region &&
                    row.IsOpen == value.IsOpen && row.OpeningTime == value.OpeningTime && row.ClosingTime == value.ClosingTime)
                    continue;

                row.Code = value.Code;
                row.Name = value.Name;
                row.Region = value.Region;
                row.IsOpen = value.IsOpen;
                row.OpeningTime = value.OpeningTime;
                row.ClosingTime = value.ClosingTime;
                PersistenceMapping.Touch(row.Audit);
            }
            else
            {
                context.Branches.Add(new BranchRow
                {
                    Id = value.Id,
                    Code = value.Code,
                    Name = value.Name,
                    Region = value.Region,
                    IsOpen = value.IsOpen,
                    OpeningTime = value.OpeningTime,
                    ClosingTime = value.ClosingTime,
                    Audit = PersistenceMapping.NewAudit()
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Branch ToDomain(BranchRow row) =>
        new(row.Id, row.Code, row.Name, row.Region, row.IsOpen, row.OpeningTime, row.ClosingTime);
}

public sealed class EfCounterTerminalRepository(ApexPostDbContext context) : ICounterTerminalRepository
{
    public async Task<IReadOnlyList<CounterTerminal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.CounterTerminals.AsNoTracking().OrderBy(row => row.Code).ToListAsync(cancellationToken);
        return rows.Select(row => new CounterTerminal(row.Id, row.BranchId, row.Code, row.Name, row.IsActive)).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<CounterTerminal> terminals, CancellationToken cancellationToken = default)
    {
        var values = terminals.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.CounterTerminals.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var value in values)
        {
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (row.BranchId == value.BranchId && row.Code == value.Code && row.Name == value.Name && row.IsActive == value.IsActive)
                    continue;
                row.BranchId = value.BranchId;
                row.Code = value.Code;
                row.Name = value.Name;
                row.IsActive = value.IsActive;
                PersistenceMapping.Touch(row.Audit);
            }
            else
            {
                context.CounterTerminals.Add(new CounterTerminalRow
                {
                    Id = value.Id,
                    BranchId = value.BranchId,
                    Code = value.Code,
                    Name = value.Name,
                    IsActive = value.IsActive,
                    Audit = PersistenceMapping.NewAudit()
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfStaffRepository(ApexPostDbContext context) : IStaffRepository
{
    public async Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.Staff.AsNoTracking().OrderBy(row => row.Email).ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<StaffMember?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var row = await context.Staff.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task SaveAllAsync(IEnumerable<StaffMember> staff, CancellationToken cancellationToken = default)
    {
        var values = staff.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.Staff.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        var roleCodes = Enum.GetValues<StaffRole>().Select(role => role.ToString()).ToArray();
        var existingRoles = await context.StaffRoles.Select(row => row.Code).ToListAsync(cancellationToken);
        foreach (var role in roleCodes.Except(existingRoles, StringComparer.Ordinal))
        {
            context.StaffRoles.Add(new StaffRoleRow
            {
                Code = role,
                Name = role switch
                {
                    nameof(StaffRole.CounterClerk) => "Counter Clerk",
                    nameof(StaffRole.BranchManager) => "Branch Manager",
                    _ => role
                },
                Audit = PersistenceMapping.NewAudit()
            });
        }

        foreach (var value in values)
        {
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (row.BranchId == value.BranchId && row.Email == value.Email && row.FullName == value.FullName &&
                    row.Role == value.Role.ToString() && row.IsActive == value.IsActive && row.PasswordHash == value.PasswordHash)
                    continue;
                row.BranchId = value.BranchId;
                row.Email = value.Email;
                row.FullName = value.FullName;
                row.Role = value.Role.ToString();
                row.IsActive = value.IsActive;
                row.PasswordHash = value.PasswordHash;
                PersistenceMapping.Touch(row.Audit);
            }
            else
            {
                context.Staff.Add(new StaffRow
                {
                    Id = value.Id,
                    BranchId = value.BranchId,
                    Email = value.Email,
                    FullName = value.FullName,
                    Role = value.Role.ToString(),
                    IsActive = value.IsActive,
                    PasswordHash = value.PasswordHash,
                    Audit = PersistenceMapping.NewAudit()
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static StaffMember ToDomain(StaffRow row) =>
        new(row.Id, row.Email, row.FullName, PersistenceMapping.ParseEnum<StaffRole>(row.Role), row.BranchId, row.IsActive, row.PasswordHash);
}

public sealed class EfProductRepository(ApexPostDbContext context) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.Products.AsNoTracking().OrderBy(row => row.Sku).ToListAsync(cancellationToken);
        return rows.Select(row => new Product(row.Id, row.Sku, row.Name, row.Description, row.Category, row.Price, row.IsActive, row.TaxCategory)).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default)
    {
        var values = products.ToArray();
        await EnsureLookupRowsAsync(values, cancellationToken);
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.Products.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var value in values)
        {
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (row.Sku == value.Sku && row.Name == value.Name && row.Description == value.Description &&
                    row.Category == value.Category && row.TaxCategory == value.TaxCategory && row.Price == value.Price && row.IsActive == value.IsActive)
                    continue;
                row.Sku = value.Sku;
                row.Name = value.Name;
                row.Description = value.Description;
                row.Category = value.Category;
                row.TaxCategory = value.TaxCategory;
                row.Price = value.Price;
                row.IsActive = value.IsActive;
                PersistenceMapping.Touch(row.Audit);
            }
            else
            {
                context.Products.Add(new ProductRow
                {
                    Id = value.Id,
                    Sku = value.Sku,
                    Name = value.Name,
                    Description = value.Description,
                    Category = value.Category,
                    TaxCategory = value.TaxCategory,
                    Price = value.Price,
                    IsActive = value.IsActive,
                    Audit = PersistenceMapping.NewAudit()
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default) =>
        new(GetVersionCoreAsync(cancellationToken));

    private async Task<string> GetVersionCoreAsync(CancellationToken cancellationToken)
    {
        var latestVersion = await context.Products.AsNoTracking()
            .OrderByDescending(row => row.RowVersion)
            .Select(row => row.RowVersion)
            .FirstOrDefaultAsync(cancellationToken);
        return latestVersion is null ? "empty" : Convert.ToHexString(latestVersion);
    }

    private async Task EnsureLookupRowsAsync(Product[] products, CancellationToken cancellationToken)
    {
        var categories = products.Select(product => product.Category).Distinct(StringComparer.Ordinal).ToArray();
        var taxCategories = products.Select(product => product.TaxCategory).Distinct(StringComparer.Ordinal).ToArray();
        var knownCategories = await context.ProductCategories.Select(row => row.Code).ToListAsync(cancellationToken);
        var knownTaxCategories = await context.TaxCategories.Select(row => row.Code).ToListAsync(cancellationToken);
        foreach (var code in categories.Except(knownCategories, StringComparer.Ordinal))
        {
            context.ProductCategories.Add(new ProductCategoryRow { Code = code, Name = code, Audit = PersistenceMapping.NewAudit() });
        }
        foreach (var code in taxCategories.Except(knownTaxCategories, StringComparer.Ordinal))
        {
            context.TaxCategories.Add(new TaxCategoryRow { Code = code, Name = code, Audit = PersistenceMapping.NewAudit() });
        }
    }
}

public sealed class EfShippingZoneRepository(ApexPostDbContext context) : IShippingZoneRepository
{
    public async Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.ShippingZones.AsNoTracking().OrderBy(row => row.Code).ToListAsync(cancellationToken);
        return rows.Select(row => new ShippingZone(row.Id, row.Code, row.Name, row.Destination, row.IsActive)).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<ShippingZone> zones, CancellationToken cancellationToken = default)
    {
        var values = zones.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.ShippingZones.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var value in values)
        {
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (row.Code == value.Code && row.Name == value.Name && row.Destination == value.Destination && row.IsActive == value.IsActive)
                    continue;
                row.Code = value.Code;
                row.Name = value.Name;
                row.Destination = value.Destination;
                row.IsActive = value.IsActive;
                PersistenceMapping.Touch(row.Audit);
            }
            else
            {
                context.ShippingZones.Add(new ShippingZoneRow
                {
                    Id = value.Id,
                    Code = value.Code,
                    Name = value.Name,
                    Destination = value.Destination,
                    IsActive = value.IsActive,
                    Audit = PersistenceMapping.NewAudit()
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfShippingRateRepository(ApexPostDbContext context) : IShippingRateRepository
{
    public async Task<IReadOnlyList<ShippingRate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.ShippingRates.AsNoTracking().OrderBy(row => row.ZoneId).ThenBy(row => row.ServiceLevel).ToListAsync(cancellationToken);
        return rows.Select(row => new ShippingRate(
            row.Id, row.ZoneId, row.ServiceLevel,
            row.BasePrice, row.PricePerKilogram, row.MaximumWeightKilograms, row.IsActive)).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<ShippingRate> rates, CancellationToken cancellationToken = default)
    {
        var values = rates.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.ShippingRates.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var value in values)
        {
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (row.ZoneId == value.ZoneId && row.ServiceLevel == value.ServiceLevel &&
                    row.BasePrice == value.BasePrice && row.PricePerKilogram == value.PricePerKilogram &&
                    row.MaximumWeightKilograms == value.MaximumWeightKilograms && row.IsActive == value.IsActive)
                    continue;
                row.ZoneId = value.ZoneId;
                row.ServiceLevel = value.ServiceLevel;
                row.BasePrice = value.BasePrice;
                row.PricePerKilogram = value.PricePerKilogram;
                row.MaximumWeightKilograms = value.MaximumWeightKilograms;
                row.IsActive = value.IsActive;
                PersistenceMapping.Touch(row.Audit);
            }
            else
            {
                context.ShippingRates.Add(new ShippingRateRow
                {
                    Id = value.Id,
                    ZoneId = value.ZoneId,
                    ServiceLevel = value.ServiceLevel,
                    BasePrice = value.BasePrice,
                    PricePerKilogram = value.PricePerKilogram,
                    MaximumWeightKilograms = value.MaximumWeightKilograms,
                    IsActive = value.IsActive,
                    Audit = PersistenceMapping.NewAudit()
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
