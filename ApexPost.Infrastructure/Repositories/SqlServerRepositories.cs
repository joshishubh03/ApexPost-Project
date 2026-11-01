using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure.Repositories;

public static class SqlServerInfrastructureExtensions
{
    public static IServiceCollection AddApexPostSqlServerInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ApexPostDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IBranchRepository, SqlServerBranchRepository>();
        services.AddScoped<IProductRepository, SqlServerProductRepository>();
        services.AddScoped<IStaffRepository, SqlServerStaffRepository>();
        services.AddScoped<IInventoryRepository, SqlServerInventoryRepository>();
        services.AddScoped<ICounterTerminalRepository, SqlServerCounterTerminalRepository>();
        services.AddScoped<IShippingZoneRepository, SqlServerShippingZoneRepository>();
        services.AddScoped<IShippingRateRepository, SqlServerShippingRateRepository>();
        services.AddScoped<ICounterTransactionRepository, SqlServerCounterTransactionRepository>();
        services.AddScoped<IMoneyOrderRepository, SqlServerMoneyOrderRepository>();
        services.AddScoped<IIdentityVerificationRepository, SqlServerIdentityVerificationRepository>();
        services.AddScoped<IAuditLogRepository, SqlServerAuditLogRepository>();
        services.AddScoped<INotificationRepository, SqlServerNotificationRepository>();
        services.AddScoped<IStoredProcedureService, SqlServerStoredProcedureService>();
        return services;
    }
}

public sealed class SqlServerBranchRepository(ApexPostDbContext dbContext) : IBranchRepository
{
    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Branches
            .AsNoTracking()
            .Select(branch => new Branch(
                branch.Id,
                branch.Code,
                branch.Name,
                branch.Region,
                branch.IsOpen,
                branch.OpeningTime,
                branch.ClosingTime))
            .ToListAsync(cancellationToken);
    }

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(branch => branch.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new Branch(entity.Id, entity.Code, entity.Name, entity.Region, entity.IsOpen, entity.OpeningTime, entity.ClosingTime);
    }

    public async Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default)
    {
        foreach (var branch in branches)
        {
            var entity = await dbContext.Branches.FirstOrDefaultAsync(x => x.Id == branch.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.Branches.Add(new BranchEntity
                {
                    Id = branch.Id,
                    Code = branch.Code,
                    Name = branch.Name,
                    Region = branch.Region,
                    IsOpen = branch.IsOpen,
                    OpeningTime = branch.OpeningTime,
                    ClosingTime = branch.ClosingTime
                });
            }
            else
            {
                entity.Code = branch.Code;
                entity.Name = branch.Name;
                entity.Region = branch.Region;
                entity.IsOpen = branch.IsOpen;
                entity.OpeningTime = branch.OpeningTime;
                entity.ClosingTime = branch.ClosingTime;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerProductRepository(ApexPostDbContext dbContext) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Select(product => new Product(
                product.Id,
                product.Sku,
                product.Name,
                product.Description,
                product.Category,
                product.Price,
                product.IsActive,
                product.TaxCategory))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default)
    {
        foreach (var product in products)
        {
            var entity = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == product.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.Products.Add(new ProductEntity
                {
                    Id = product.Id,
                    Sku = product.Sku,
                    Name = product.Name,
                    Description = product.Description,
                    Category = product.Category,
                    TaxCategory = product.TaxCategory,
                    Price = product.Price,
                    IsActive = product.IsActive
                });
            }
            else
            {
                entity.Sku = product.Sku;
                entity.Name = product.Name;
                entity.Description = product.Description;
                entity.Category = product.Category;
                entity.TaxCategory = product.TaxCategory;
                entity.Price = product.Price;
                entity.IsActive = product.IsActive;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult("SqlServer");
    }
}

public sealed class SqlServerStaffRepository(ApexPostDbContext dbContext) : IStaffRepository
{
    public async Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Staff
            .AsNoTracking()
            .Select(staff => new StaffMember(
                staff.Id,
                staff.Email,
                staff.FullName,
                staff.Role,
                staff.BranchId,
                staff.IsActive,
                staff.PasswordHash))
            .ToListAsync(cancellationToken);
    }

    public async Task<StaffMember?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Staff.AsNoTracking().FirstOrDefaultAsync(staff => staff.Email == email, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new StaffMember(entity.Id, entity.Email, entity.FullName, entity.Role, entity.BranchId, entity.IsActive, entity.PasswordHash);
    }

    public async Task SaveAllAsync(IEnumerable<StaffMember> staff, CancellationToken cancellationToken = default)
    {
        foreach (var member in staff)
        {
            var entity = await dbContext.Staff.FirstOrDefaultAsync(x => x.Id == member.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.Staff.Add(new StaffMemberEntity
                {
                    Id = member.Id,
                    Email = member.Email,
                    FullName = member.FullName,
                    Role = member.Role,
                    BranchId = member.BranchId,
                    IsActive = member.IsActive,
                    PasswordHash = member.PasswordHash
                });
            }
            else
            {
                entity.Email = member.Email;
                entity.FullName = member.FullName;
                entity.Role = member.Role;
                entity.BranchId = member.BranchId;
                entity.IsActive = member.IsActive;
                entity.PasswordHash = member.PasswordHash;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerInventoryRepository(ApexPostDbContext dbContext) : IInventoryRepository
{
    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Inventory.AsNoTracking().ToListAsync(cancellationToken);
        return entities
            .Select(entity => new InventoryItem(
                entity.BranchId,
                entity.ProductId,
                entity.QuantityOnHand,
                entity.ReorderLevel,
                DeserializeMovements(entity.MovementsJson)))
            .ToList();
    }

    public async Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            var entity = await dbContext.Inventory.FirstOrDefaultAsync(x => x.BranchId == item.BranchId && x.ProductId == item.ProductId, cancellationToken);
            if (entity is null)
            {
                dbContext.Inventory.Add(new InventoryItemEntity
                {
                    BranchId = item.BranchId,
                    ProductId = item.ProductId,
                    QuantityOnHand = item.QuantityOnHand,
                    ReorderLevel = item.ReorderLevel,
                    MovementsJson = JsonSerializer.Serialize(item.Movements)
                });
            }
            else
            {
                entity.QuantityOnHand = item.QuantityOnHand;
                entity.ReorderLevel = item.ReorderLevel;
                entity.MovementsJson = JsonSerializer.Serialize(item.Movements);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<InventoryMovement> DeserializeMovements(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<InventoryMovement>();
        }

        return JsonSerializer.Deserialize<List<InventoryMovement>>(value) ?? new List<InventoryMovement>();
    }
}

public sealed class SqlServerShippingZoneRepository(ApexPostDbContext dbContext) : IShippingZoneRepository
{
    public async Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ShippingZones
            .AsNoTracking()
            .Select(zone => new ShippingZone(zone.Id, zone.Code, zone.Name, zone.Destination, zone.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IEnumerable<ShippingZone> zones, CancellationToken cancellationToken = default)
    {
        foreach (var zone in zones)
        {
            var entity = await dbContext.ShippingZones.FirstOrDefaultAsync(x => x.Id == zone.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.ShippingZones.Add(new ShippingZoneEntity
                {
                    Id = zone.Id,
                    Code = zone.Code,
                    Name = zone.Name,
                    Destination = zone.Destination,
                    IsActive = zone.IsActive
                });
            }
            else
            {
                entity.Code = zone.Code;
                entity.Name = zone.Name;
                entity.Destination = zone.Destination;
                entity.IsActive = zone.IsActive;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerShippingRateRepository(ApexPostDbContext dbContext) : IShippingRateRepository
{
    public async Task<IReadOnlyList<ShippingRate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ShippingRates
            .AsNoTracking()
            .Select(rate => new ShippingRate(
                rate.Id,
                rate.ZoneId,
                rate.ServiceLevel,
                rate.BasePrice,
                rate.PricePerKilogram,
                rate.MaximumWeightKilograms,
                rate.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IEnumerable<ShippingRate> rates, CancellationToken cancellationToken = default)
    {
        foreach (var rate in rates)
        {
            var entity = await dbContext.ShippingRates.FirstOrDefaultAsync(x => x.Id == rate.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.ShippingRates.Add(new ShippingRateEntity
                {
                    Id = rate.Id,
                    ZoneId = rate.ZoneId,
                    ServiceLevel = rate.ServiceLevel,
                    BasePrice = rate.BasePrice,
                    PricePerKilogram = rate.PricePerKilogram,
                    MaximumWeightKilograms = rate.MaximumWeightKilograms,
                    IsActive = rate.IsActive
                });
            }
            else
            {
                entity.ZoneId = rate.ZoneId;
                entity.ServiceLevel = rate.ServiceLevel;
                entity.BasePrice = rate.BasePrice;
                entity.PricePerKilogram = rate.PricePerKilogram;
                entity.MaximumWeightKilograms = rate.MaximumWeightKilograms;
                entity.IsActive = rate.IsActive;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerCounterTerminalRepository(ApexPostDbContext dbContext) : ICounterTerminalRepository
{
    public async Task<IReadOnlyList<CounterTerminal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.CounterTerminals
            .AsNoTracking()
            .Select(terminal => new CounterTerminal(terminal.Id, terminal.BranchId, terminal.Code, terminal.Name, terminal.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IEnumerable<CounterTerminal> terminals, CancellationToken cancellationToken = default)
    {
        foreach (var terminal in terminals)
        {
            var entity = await dbContext.CounterTerminals.FirstOrDefaultAsync(x => x.Id == terminal.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.CounterTerminals.Add(new CounterTerminalEntity
                {
                    Id = terminal.Id,
                    BranchId = terminal.BranchId,
                    Code = terminal.Code,
                    Name = terminal.Name,
                    IsActive = terminal.IsActive
                });
            }
            else
            {
                entity.BranchId = terminal.BranchId;
                entity.Code = terminal.Code;
                entity.Name = terminal.Name;
                entity.IsActive = terminal.IsActive;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerCounterTransactionRepository(ApexPostDbContext dbContext) : ICounterTransactionRepository
{
    public async Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.CounterTransactions.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(MapTransaction).ToList();
    }

    public async Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default)
    {
        foreach (var transaction in transactions)
        {
            var entity = await dbContext.CounterTransactions.FirstOrDefaultAsync(x => x.Id == transaction.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.CounterTransactions.Add(new CounterTransactionEntity
                {
                    Id = transaction.Id,
                    TransactionNumber = transaction.TransactionNumber,
                    BranchId = transaction.BranchId,
                    StaffId = transaction.StaffId,
                    TerminalId = transaction.TerminalId,
                    MoneyOrderId = transaction.MoneyOrderId,
                    MoneyOrderAmount = transaction.MoneyOrderAmount,
                    MoneyOrderFee = transaction.MoneyOrderFee,
                    OccurredAtUtc = transaction.OccurredAt,
                    LinesJson = JsonSerializer.Serialize(transaction.Lines),
                    ShippingRateId = transaction.ShippingRateId,
                    ShippingZoneName = transaction.ShippingZoneName,
                    ShippingServiceLevel = transaction.ShippingServiceLevel,
                    ShipmentType = transaction.ShipmentType,
                    ShippingWeightKilograms = transaction.ShippingWeightKilograms,
                    Subtotal = transaction.Subtotal,
                    TaxTotal = transaction.TaxTotal,
                    ShippingCharge = transaction.ShippingCharge,
                    ShippingTaxRatePercent = transaction.ShippingTaxRatePercent,
                    ShippingTaxAmount = transaction.ShippingTaxAmount,
                    TotalAmount = transaction.TotalAmount,
                    PaymentMethod = transaction.PaymentMethod,
                    Status = transaction.Status,
                    ResolvedAtUtc = transaction.ResolvedAt,
                    ResolvedByStaffId = transaction.ResolvedByStaffId,
                    ResolutionReason = transaction.ResolutionReason,
                    ShippingRecipientName = transaction.ShippingRecipientName,
                    ShippingAddress = transaction.ShippingAddress,
                    ParcelLengthCentimeters = transaction.ParcelLengthCentimeters,
                    ParcelWidthCentimeters = transaction.ParcelWidthCentimeters,
                    ParcelHeightCentimeters = transaction.ParcelHeightCentimeters
                });
            }
            else
            {
                entity.TransactionNumber = transaction.TransactionNumber;
                entity.BranchId = transaction.BranchId;
                entity.StaffId = transaction.StaffId;
                entity.TerminalId = transaction.TerminalId;
                entity.MoneyOrderId = transaction.MoneyOrderId;
                entity.MoneyOrderAmount = transaction.MoneyOrderAmount;
                entity.MoneyOrderFee = transaction.MoneyOrderFee;
                entity.OccurredAtUtc = transaction.OccurredAt;
                entity.LinesJson = JsonSerializer.Serialize(transaction.Lines);
                entity.ShippingRateId = transaction.ShippingRateId;
                entity.ShippingZoneName = transaction.ShippingZoneName;
                entity.ShippingServiceLevel = transaction.ShippingServiceLevel;
                entity.ShipmentType = transaction.ShipmentType;
                entity.ShippingWeightKilograms = transaction.ShippingWeightKilograms;
                entity.Subtotal = transaction.Subtotal;
                entity.TaxTotal = transaction.TaxTotal;
                entity.ShippingCharge = transaction.ShippingCharge;
                entity.ShippingTaxRatePercent = transaction.ShippingTaxRatePercent;
                entity.ShippingTaxAmount = transaction.ShippingTaxAmount;
                entity.TotalAmount = transaction.TotalAmount;
                entity.PaymentMethod = transaction.PaymentMethod;
                entity.Status = transaction.Status;
                entity.ResolvedAtUtc = transaction.ResolvedAt;
                entity.ResolvedByStaffId = transaction.ResolvedByStaffId;
                entity.ResolutionReason = transaction.ResolutionReason;
                entity.ShippingRecipientName = transaction.ShippingRecipientName;
                entity.ShippingAddress = transaction.ShippingAddress;
                entity.ParcelLengthCentimeters = transaction.ParcelLengthCentimeters;
                entity.ParcelWidthCentimeters = transaction.ParcelWidthCentimeters;
                entity.ParcelHeightCentimeters = transaction.ParcelHeightCentimeters;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CounterTransaction MapTransaction(CounterTransactionEntity entity)
    {
        IReadOnlyList<TransactionLine> lines = string.IsNullOrWhiteSpace(entity.LinesJson)
            ? Array.Empty<TransactionLine>()
            : JsonSerializer.Deserialize<List<TransactionLine>>(entity.LinesJson) ?? new List<TransactionLine>();

        return new CounterTransaction(
            entity.Id,
            entity.TransactionNumber,
            entity.BranchId,
            entity.StaffId,
            entity.OccurredAtUtc,
            lines,
            entity.ShippingRateId,
            entity.ShippingZoneName,
            entity.ShippingServiceLevel,
            entity.ShippingWeightKilograms,
            entity.ShippingCharge,
            entity.PaymentMethod,
            entity.Status,
            entity.ResolvedAtUtc,
            entity.ResolvedByStaffId,
            entity.ResolutionReason,
            entity.ShippingRecipientName,
            entity.ShippingAddress,
            entity.ParcelLengthCentimeters,
            entity.ParcelWidthCentimeters,
            entity.ParcelHeightCentimeters,
            entity.ShippingTaxRatePercent,
            entity.TerminalId,
            entity.MoneyOrderId,
            entity.MoneyOrderAmount,
            entity.MoneyOrderFee,
            entity.ShipmentType);
    }
}

public sealed class SqlServerMoneyOrderRepository(ApexPostDbContext dbContext) : IMoneyOrderRepository
{
    public async Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.MoneyOrders
            .AsNoTracking()
            .Select(mo => new MoneyOrder(
                mo.Id,
                mo.MoneyOrderNumber,
                mo.BranchId,
                mo.StaffId,
                mo.OccurredAtUtc,
                mo.SenderName,
                mo.RecipientName,
                mo.Amount,
                mo.Fee,
                mo.VerificationStatus,
                mo.VerificationRecordId,
                mo.DocumentType,
                mo.MaskedDocumentIdentifier,
                mo.Status,
                mo.RefundedAtUtc,
                mo.RefundedByStaffId,
                mo.RefundReason))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default)
    {
        foreach (var moneyOrder in moneyOrders)
        {
            var entity = await dbContext.MoneyOrders.FirstOrDefaultAsync(x => x.Id == moneyOrder.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.MoneyOrders.Add(new MoneyOrderEntity
                {
                    Id = moneyOrder.Id,
                    MoneyOrderNumber = moneyOrder.MoneyOrderNumber,
                    BranchId = moneyOrder.BranchId,
                    StaffId = moneyOrder.StaffId,
                    OccurredAtUtc = moneyOrder.OccurredAt,
                    SenderName = moneyOrder.SenderName,
                    RecipientName = moneyOrder.RecipientName,
                    Amount = moneyOrder.Amount,
                    Fee = moneyOrder.Fee,
                    TotalAmount = moneyOrder.TotalAmount,
                    VerificationStatus = moneyOrder.VerificationStatus,
                    VerificationRecordId = moneyOrder.VerificationRecordId,
                    DocumentType = moneyOrder.DocumentType,
                    MaskedDocumentIdentifier = moneyOrder.MaskedDocumentIdentifier,
                    Status = moneyOrder.Status,
                    RefundedAtUtc = moneyOrder.RefundedAt,
                    RefundedByStaffId = moneyOrder.RefundedByStaffId,
                    RefundReason = moneyOrder.RefundReason
                });
            }
            else
            {
                entity.MoneyOrderNumber = moneyOrder.MoneyOrderNumber;
                entity.BranchId = moneyOrder.BranchId;
                entity.StaffId = moneyOrder.StaffId;
                entity.OccurredAtUtc = moneyOrder.OccurredAt;
                entity.SenderName = moneyOrder.SenderName;
                entity.RecipientName = moneyOrder.RecipientName;
                entity.Amount = moneyOrder.Amount;
                entity.Fee = moneyOrder.Fee;
                entity.TotalAmount = moneyOrder.TotalAmount;
                entity.VerificationStatus = moneyOrder.VerificationStatus;
                entity.VerificationRecordId = moneyOrder.VerificationRecordId;
                entity.DocumentType = moneyOrder.DocumentType;
                entity.MaskedDocumentIdentifier = moneyOrder.MaskedDocumentIdentifier;
                entity.Status = moneyOrder.Status;
                entity.RefundedAtUtc = moneyOrder.RefundedAt;
                entity.RefundedByStaffId = moneyOrder.RefundedByStaffId;
                entity.RefundReason = moneyOrder.RefundReason;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerIdentityVerificationRepository(ApexPostDbContext dbContext) : IIdentityVerificationRepository
{
    public async Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.IdentityVerifications
            .AsNoTracking()
            .Select(record => new IdentityVerificationRecord(
                record.Id,
                record.BranchId,
                record.StaffId,
                record.OccurredAtUtc,
                record.DocumentType,
                record.MaskedDocumentIdentifier,
                record.MoneyOrderAmount,
                record.Status,
                record.CorrelationId))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            var entity = await dbContext.IdentityVerifications.FirstOrDefaultAsync(x => x.Id == record.Id, cancellationToken);
            if (entity is null)
            {
                dbContext.IdentityVerifications.Add(new IdentityVerificationEntity
                {
                    Id = record.Id,
                    BranchId = record.BranchId,
                    StaffId = record.StaffId,
                    OccurredAtUtc = record.OccurredAt,
                    DocumentType = record.DocumentType,
                    MaskedDocumentIdentifier = record.MaskedDocumentIdentifier,
                    MoneyOrderAmount = record.MoneyOrderAmount,
                    Status = record.Status,
                    CorrelationId = record.CorrelationId
                });
            }
            else
            {
                entity.BranchId = record.BranchId;
                entity.StaffId = record.StaffId;
                entity.OccurredAtUtc = record.OccurredAt;
                entity.DocumentType = record.DocumentType;
                entity.MaskedDocumentIdentifier = record.MaskedDocumentIdentifier;
                entity.MoneyOrderAmount = record.MoneyOrderAmount;
                entity.Status = record.Status;
                entity.CorrelationId = record.CorrelationId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerAuditLogRepository(ApexPostDbContext dbContext) : IAuditLogRepository
{
    public async Task<IReadOnlyList<AuditRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AuditEntries
            .AsNoTracking()
            .Select(entry => new AuditRecord(
                entry.Id,
                entry.OccurredAtUtc,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.ActorId,
                entry.BranchId,
                entry.CorrelationId,
                entry.Amount,
                entry.Details,
                entry.Role,
                entry.ResultStatusCode,
                entry.DurationMilliseconds,
                string.IsNullOrWhiteSpace(entry.RouteValuesJson)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(entry.RouteValuesJson)))
            .ToListAsync(cancellationToken);
    }

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        dbContext.AuditEntries.Add(new AuditEntryEntity
        {
            Id = record.Id,
            OccurredAtUtc = record.OccurredAt,
            Action = record.Action,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            ActorId = record.ActorId,
            BranchId = record.BranchId,
            CorrelationId = record.CorrelationId,
            Amount = record.Amount,
            Details = record.Details,
            Role = record.Role,
            ResultStatusCode = record.ResultStatusCode,
            DurationMilliseconds = record.DurationMilliseconds,
            RouteValuesJson = record.RouteValues is null ? null : JsonSerializer.Serialize(record.RouteValues)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerNotificationRepository(ApexPostDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyList<OperationalNotification>> GetForBranchAsync(
        Guid branchId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OperationalNotifications
            .AsNoTracking()
            .Where(notification => notification.BranchId == branchId)
            .OrderByDescending(notification => notification.OccurredAtUtc)
            .Take(limit)
            .Select(notification => new OperationalNotification(
                notification.Id,
                notification.Type,
                notification.Message,
                notification.BranchId,
                notification.ActorId,
                notification.OccurredAtUtc,
                notification.CorrelationId,
                notification.ReferenceId))
            .ToListAsync(cancellationToken);
    }

    public async Task AppendAsync(OperationalNotification notification, CancellationToken cancellationToken = default)
    {
        dbContext.OperationalNotifications.Add(new OperationalNotificationEntity
        {
            Id = notification.Id,
            Type = notification.Type,
            Message = notification.Message,
            BranchId = notification.BranchId,
            ActorId = notification.ActorId,
            OccurredAtUtc = notification.OccurredAt,
            CorrelationId = notification.CorrelationId,
            ReferenceId = notification.ReferenceId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
