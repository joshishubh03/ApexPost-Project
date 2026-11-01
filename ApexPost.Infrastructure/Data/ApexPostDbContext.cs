using System.Text.Json;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Data;

public sealed class ApexPostDbContext : DbContext
{
    public DbSet<BranchEntity> Branches => Set<BranchEntity>();
    public DbSet<StaffMemberEntity> Staff => Set<StaffMemberEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<InventoryItemEntity> Inventory => Set<InventoryItemEntity>();
    public DbSet<ShippingZoneEntity> ShippingZones => Set<ShippingZoneEntity>();
    public DbSet<ShippingRateEntity> ShippingRates => Set<ShippingRateEntity>();
    public DbSet<CounterTerminalEntity> CounterTerminals => Set<CounterTerminalEntity>();
    public DbSet<CounterTransactionEntity> CounterTransactions => Set<CounterTransactionEntity>();
    public DbSet<MoneyOrderEntity> MoneyOrders => Set<MoneyOrderEntity>();
    public DbSet<IdentityVerificationEntity> IdentityVerifications => Set<IdentityVerificationEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<OperationalNotificationEntity> OperationalNotifications => Set<OperationalNotificationEntity>();

    public ApexPostDbContext(DbContextOptions<ApexPostDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureBranch(modelBuilder.Entity<BranchEntity>());
        ConfigureStaff(modelBuilder.Entity<StaffMemberEntity>());
        ConfigureProduct(modelBuilder.Entity<ProductEntity>());
        ConfigureInventory(modelBuilder.Entity<InventoryItemEntity>());
        ConfigureShippingZone(modelBuilder.Entity<ShippingZoneEntity>());
        ConfigureShippingRate(modelBuilder.Entity<ShippingRateEntity>());
        ConfigureCounterTerminal(modelBuilder.Entity<CounterTerminalEntity>());
        ConfigureCounterTransaction(modelBuilder.Entity<CounterTransactionEntity>());
        ConfigureMoneyOrder(modelBuilder.Entity<MoneyOrderEntity>());
        ConfigureIdentityVerification(modelBuilder.Entity<IdentityVerificationEntity>());
        ConfigureAuditEntry(modelBuilder.Entity<AuditEntryEntity>());
        ConfigureOperationalNotification(modelBuilder.Entity<OperationalNotificationEntity>());
    }

    private static void ConfigureBranch(EntityTypeBuilder<BranchEntity> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Region).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OpeningTime).HasColumnType("time");
        builder.Property(x => x.ClosingTime).HasColumnType("time");
    }

    private static void ConfigureStaff(EntityTypeBuilder<StaffMemberEntity> builder)
    {
        builder.ToTable("Staff");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Role).HasConversion<string>();
    }

    private static void ConfigureProduct(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TaxCategory).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Price).HasColumnType("decimal(18,2)");
    }

    private static void ConfigureInventory(EntityTypeBuilder<InventoryItemEntity> builder)
    {
        builder.ToTable("Inventory");
        builder.HasKey(x => new { x.BranchId, x.ProductId });
        builder.Property(x => x.MovementsJson).HasColumnType("nvarchar(max)");
    }

    private static void ConfigureShippingZone(EntityTypeBuilder<ShippingZoneEntity> builder)
    {
        builder.ToTable("ShippingZones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Destination).HasMaxLength(200).IsRequired();
    }

    private static void ConfigureShippingRate(EntityTypeBuilder<ShippingRateEntity> builder)
    {
        builder.ToTable("ShippingRates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceLevel).HasConversion<string>();
        builder.Property(x => x.BasePrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PricePerKilogram).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaximumWeightKilograms).HasColumnType("decimal(18,2)");
    }

    private static void ConfigureCounterTerminal(EntityTypeBuilder<CounterTerminalEntity> builder)
    {
        builder.ToTable("CounterTerminals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
    }

    private static void ConfigureCounterTransaction(EntityTypeBuilder<CounterTransactionEntity> builder)
    {
        builder.ToTable("CounterTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.ShippingServiceLevel).HasConversion<string>();
        builder.Property(x => x.ShipmentType).HasConversion<string>();
        builder.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TaxTotal).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ShippingCharge).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ShippingTaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MoneyOrderAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MoneyOrderFee).HasColumnType("decimal(18,2)");
        builder.Property(x => x.LinesJson).HasColumnType("nvarchar(max)");
    }

    private static void ConfigureMoneyOrder(EntityTypeBuilder<MoneyOrderEntity> builder)
    {
        builder.ToTable("MoneyOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MoneyOrderNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.VerificationStatus).HasConversion<string>();
        builder.Property(x => x.DocumentType).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Fee).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
    }

    private static void ConfigureIdentityVerification(EntityTypeBuilder<IdentityVerificationEntity> builder)
    {
        builder.ToTable("IdentityVerifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentType).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.MaskedDocumentIdentifier).HasMaxLength(32).IsRequired();
        builder.Property(x => x.MoneyOrderAmount).HasColumnType("decimal(18,2)");
    }

    private static void ConfigureAuditEntry(EntityTypeBuilder<AuditEntryEntity> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Details).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RouteValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
    }

    private static void ConfigureOperationalNotification(EntityTypeBuilder<OperationalNotificationEntity> builder)
    {
        builder.ToTable("OperationalNotifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReferenceId).HasMaxLength(200);
    }
}

public sealed class BranchEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
}

public sealed class StaffMemberEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public StaffRole Role { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
}

public sealed class ProductEntity
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TaxCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

public sealed class InventoryItemEntity
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string MovementsJson { get; set; } = "[]";
}

public sealed class ShippingZoneEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ShippingRateEntity
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public ShippingServiceLevel ServiceLevel { get; set; }
    public decimal BasePrice { get; set; }
    public decimal PricePerKilogram { get; set; }
    public decimal MaximumWeightKilograms { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CounterTerminalEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CounterTransactionEntity
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid StaffId { get; set; }
    public Guid? TerminalId { get; set; }
    public Guid? MoneyOrderId { get; set; }
    public decimal MoneyOrderAmount { get; set; }
    public decimal MoneyOrderFee { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string LinesJson { get; set; } = "[]";
    public Guid? ShippingRateId { get; set; }
    public string? ShippingZoneName { get; set; }
    public ShippingServiceLevel? ShippingServiceLevel { get; set; }
    public ShipmentType? ShipmentType { get; set; }
    public decimal? ShippingWeightKilograms { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ShippingCharge { get; set; }
    public decimal ShippingTaxRatePercent { get; set; }
    public decimal ShippingTaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByStaffId { get; set; }
    public string? ResolutionReason { get; set; }
    public string? ShippingRecipientName { get; set; }
    public string? ShippingAddress { get; set; }
    public decimal? ParcelLengthCentimeters { get; set; }
    public decimal? ParcelWidthCentimeters { get; set; }
    public decimal? ParcelHeightCentimeters { get; set; }
}

public sealed class MoneyOrderEntity
{
    public Guid Id { get; set; }
    public string MoneyOrderNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid StaffId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal TotalAmount { get; set; }
    public IdentityVerificationStatus VerificationStatus { get; set; }
    public Guid? VerificationRecordId { get; set; }
    public IdentityDocumentType? DocumentType { get; set; }
    public string? MaskedDocumentIdentifier { get; set; }
    public MoneyOrderStatus Status { get; set; }
    public DateTimeOffset? RefundedAtUtc { get; set; }
    public Guid? RefundedByStaffId { get; set; }
    public string? RefundReason { get; set; }
}

public sealed class IdentityVerificationEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid StaffId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public IdentityDocumentType DocumentType { get; set; }
    public string MaskedDocumentIdentifier { get; set; } = string.Empty;
    public decimal MoneyOrderAmount { get; set; }
    public IdentityVerificationStatus Status { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class AuditEntryEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public Guid BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Details { get; set; }
    public string? Role { get; set; }
    public int? ResultStatusCode { get; set; }
    public long? DurationMilliseconds { get; set; }
    public string? RouteValuesJson { get; set; }
}

public sealed class OperationalNotificationEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
}
