using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>Relational write model. Domain records are mapped at repository boundaries.</summary>
public sealed class ApexPostDbContext(DbContextOptions<ApexPostDbContext> options) : DbContext(options)
{
    public DbSet<BranchRow> Branches => Set<BranchRow>();
    public DbSet<CounterTerminalRow> CounterTerminals => Set<CounterTerminalRow>();
    public DbSet<StaffRow> Staff => Set<StaffRow>();
    public DbSet<StaffRoleRow> StaffRoles => Set<StaffRoleRow>();
    public DbSet<ProductRow> Products => Set<ProductRow>();
    public DbSet<ProductCategoryRow> ProductCategories => Set<ProductCategoryRow>();
    public DbSet<TaxCategoryRow> TaxCategories => Set<TaxCategoryRow>();
    public DbSet<BranchStockRow> BranchStock => Set<BranchStockRow>();
    public DbSet<StockAdjustmentRow> StockAdjustments => Set<StockAdjustmentRow>();
    public DbSet<ShippingZoneRow> ShippingZones => Set<ShippingZoneRow>();
    public DbSet<ShippingRateRow> ShippingRates => Set<ShippingRateRow>();
    public DbSet<IdentityVerificationRow> IdentityVerifications => Set<IdentityVerificationRow>();
    public DbSet<MoneyOrderRow> MoneyOrders => Set<MoneyOrderRow>();
    public DbSet<CounterTransactionRow> CounterTransactions => Set<CounterTransactionRow>();
    public DbSet<TransactionLineRow> TransactionLines => Set<TransactionLineRow>();
    public DbSet<PaymentRow> Payments => Set<PaymentRow>();
    public DbSet<AuditEntryRow> AuditEntries => Set<AuditEntryRow>();
    public DbSet<OperationalNotificationRow> OperationalNotifications => Set<OperationalNotificationRow>();
    public DbSet<OutboxMessageRow> OutboxMessages => Set<OutboxMessageRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureBranches(modelBuilder.Entity<BranchRow>());
        ConfigureCounterTerminals(modelBuilder.Entity<CounterTerminalRow>());
        ConfigureStaff(modelBuilder.Entity<StaffRow>());
        ConfigureStaffRoles(modelBuilder.Entity<StaffRoleRow>());
        ConfigureProducts(modelBuilder.Entity<ProductRow>());
        ConfigureProductCategories(modelBuilder.Entity<ProductCategoryRow>());
        ConfigureTaxCategories(modelBuilder.Entity<TaxCategoryRow>());
        ConfigureBranchStock(modelBuilder.Entity<BranchStockRow>());
        ConfigureStockAdjustments(modelBuilder.Entity<StockAdjustmentRow>());
        ConfigureShippingZones(modelBuilder.Entity<ShippingZoneRow>());
        ConfigureShippingRates(modelBuilder.Entity<ShippingRateRow>());
        ConfigureIdentityVerifications(modelBuilder.Entity<IdentityVerificationRow>());
        ConfigureMoneyOrders(modelBuilder.Entity<MoneyOrderRow>());
        ConfigureCounterTransactions(modelBuilder.Entity<CounterTransactionRow>());
        ConfigureTransactionLines(modelBuilder.Entity<TransactionLineRow>());
        ConfigurePayments(modelBuilder.Entity<PaymentRow>());
        ConfigureAuditEntries(modelBuilder.Entity<AuditEntryRow>());
        ConfigureNotifications(modelBuilder.Entity<OperationalNotificationRow>());
        ConfigureOutbox(modelBuilder.Entity<OutboxMessageRow>());

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureBranches(EntityTypeBuilder<BranchRow> entity)
    {
        entity.ToTable("Branches", table => table.HasCheckConstraint(
            "CK_Branches_OpeningHours", "[OpeningTime] < [ClosingTime]"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(12).IsUnicode(false).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Region).HasMaxLength(80).IsRequired();
        entity.Property(x => x.OpeningTime).HasColumnType("time(0)");
        entity.Property(x => x.ClosingTime).HasColumnType("time(0)");
        entity.HasIndex(x => x.Code).IsUnique();
        ConfigureAudit(entity);
    }

    private static void ConfigureCounterTerminals(EntityTypeBuilder<CounterTerminalRow> entity)
    {
        entity.ToTable("CounterTerminals");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(12).IsUnicode(false).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        entity.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureStaff(EntityTypeBuilder<StaffRow> entity)
    {
        entity.ToTable("Staff");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Email).HasMaxLength(254).IsUnicode(false).IsRequired();
        entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(x => x.PasswordHash).HasMaxLength(512).IsUnicode(false).IsRequired();
        entity.HasIndex(x => x.Email).IsUnique();
        entity.HasIndex(x => new { x.BranchId, x.Role, x.IsActive });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRoleRow>().WithMany().HasForeignKey(x => x.Role).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureStaffRoles(EntityTypeBuilder<StaffRoleRow> entity)
    {
        entity.ToTable("StaffRoles");
        entity.HasKey(x => x.Code);
        entity.Property(x => x.Code).HasMaxLength(32).IsUnicode(false);
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        ConfigureAudit(entity);
    }

    private static void ConfigureProducts(EntityTypeBuilder<ProductRow> entity)
    {
        entity.ToTable("Products", table => table.HasCheckConstraint("CK_Products_Price", "[Price] >= 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Sku).HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
        entity.Property(x => x.Category).HasMaxLength(60).IsRequired();
        entity.Property(x => x.TaxCategory).HasMaxLength(60).IsRequired();
        entity.Property(x => x.Price).HasPrecision(18, 2);
        entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasIndex(x => x.Sku).IsUnique();
        entity.HasIndex(x => x.RowVersion);
        entity.HasOne<ProductCategoryRow>().WithMany().HasForeignKey(x => x.Category).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<TaxCategoryRow>().WithMany().HasForeignKey(x => x.TaxCategory).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureProductCategories(EntityTypeBuilder<ProductCategoryRow> entity)
    {
        entity.ToTable("ProductCategories");
        entity.HasKey(x => x.Code);
        entity.Property(x => x.Code).HasMaxLength(60);
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        ConfigureAudit(entity);
    }

    private static void ConfigureTaxCategories(EntityTypeBuilder<TaxCategoryRow> entity)
    {
        entity.ToTable("TaxCategories");
        entity.HasKey(x => x.Code);
        entity.Property(x => x.Code).HasMaxLength(60);
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        ConfigureAudit(entity);
    }

    private static void ConfigureBranchStock(EntityTypeBuilder<BranchStockRow> entity)
    {
        entity.ToTable("BranchStock", table =>
        {
            table.HasCheckConstraint("CK_BranchStock_QuantityOnHand", "[QuantityOnHand] >= 0");
            table.HasCheckConstraint("CK_BranchStock_ReorderLevel", "[ReorderLevel] >= 0");
        });
        entity.HasKey(x => new { x.BranchId, x.ProductId });
        entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ProductRow>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.BranchId, x.QuantityOnHand });
        ConfigureAudit(entity);
    }

    private static void ConfigureStockAdjustments(EntityTypeBuilder<StockAdjustmentRow> entity)
    {
        entity.ToTable("StockAdjustments", table => table.HasCheckConstraint(
            "CK_StockAdjustments_NonZero", "[QuantityChange] <> 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Reason).HasMaxLength(250).IsRequired();
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ProductRow>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureShippingZones(EntityTypeBuilder<ShippingZoneRow> entity)
    {
        entity.ToTable("ShippingZones");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        entity.Property(x => x.Destination).HasMaxLength(120).IsRequired();
        entity.HasIndex(x => x.Code).IsUnique();
        ConfigureAudit(entity);
    }

    private static void ConfigureShippingRates(EntityTypeBuilder<ShippingRateRow> entity)
    {
        entity.ToTable("ShippingRates", table =>
        {
            table.HasCheckConstraint("CK_ShippingRates_Prices", "[BasePrice] >= 0 AND [PricePerKilogram] >= 0");
            table.HasCheckConstraint("CK_ShippingRates_MaximumWeight", "[MaximumWeightKilograms] > 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ServiceLevel).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.BasePrice).HasPrecision(18, 2);
        entity.Property(x => x.PricePerKilogram).HasPrecision(18, 2);
        entity.Property(x => x.MaximumWeightKilograms).HasPrecision(9, 3);
        entity.HasIndex(x => new { x.ZoneId, x.ServiceLevel }).IsUnique();
        entity.HasOne<ShippingZoneRow>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureIdentityVerifications(EntityTypeBuilder<IdentityVerificationRow> entity)
    {
        entity.ToTable("IdentityVerifications", table => table.HasCheckConstraint(
            "CK_IdentityVerifications_MaskedIdentifier", "LEN([MaskedDocumentIdentifier]) <= 16"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        entity.Property(x => x.MaskedDocumentIdentifier).HasMaxLength(16).IsUnicode(false).IsRequired();
        entity.Property(x => x.MoneyOrderAmount).HasPrecision(18, 2);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureMoneyOrders(EntityTypeBuilder<MoneyOrderRow> entity)
    {
        entity.ToTable("MoneyOrders", table =>
        {
            table.HasCheckConstraint("CK_MoneyOrders_Amounts", "[Amount] > 0 AND [Fee] >= 0");
            table.HasCheckConstraint("CK_MoneyOrders_MaskedIdentifier", "[MaskedDocumentIdentifier] IS NULL OR LEN([MaskedDocumentIdentifier]) <= 16");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.MoneyOrderNumber).HasMaxLength(40).IsUnicode(false).IsRequired();
        entity.Property(x => x.SenderName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.RecipientName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Fee).HasPrecision(18, 2);
        entity.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        entity.Property(x => x.MaskedDocumentIdentifier).HasMaxLength(16).IsUnicode(false);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.RefundReason).HasMaxLength(250);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.RefundedAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => x.MoneyOrderNumber).IsUnique();
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<IdentityVerificationRow>().WithMany().HasForeignKey(x => x.IdentityVerificationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CounterTransactionRow>().WithMany().HasForeignKey(x => x.CounterTransactionId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureCounterTransactions(EntityTypeBuilder<CounterTransactionRow> entity)
    {
        entity.ToTable("CounterTransactions", table =>
        {
            table.HasCheckConstraint("CK_CounterTransactions_Totals", "[Subtotal] >= 0 AND [TaxTotal] >= 0 AND [ShippingCharge] >= 0 AND [TotalAmount] >= 0");
            table.HasCheckConstraint("CK_CounterTransactions_Reversal", "([Status] IN ('Voided','Refunded') AND [ResolvedAt] IS NOT NULL AND [ResolvedByStaffId] IS NOT NULL AND [ResolutionReason] IS NOT NULL) OR ([Status] NOT IN ('Voided','Refunded') AND [ResolvedAt] IS NULL AND [ResolvedByStaffId] IS NULL AND [ResolutionReason] IS NULL)");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.TransactionNumber).HasMaxLength(40).IsUnicode(false).IsRequired();
        entity.Property(x => x.ShippingZoneName).HasMaxLength(80);
        entity.Property(x => x.ShippingServiceLevel).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.ShipmentType).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.Subtotal).HasPrecision(18, 2);
        entity.Property(x => x.TaxTotal).HasPrecision(18, 2);
        entity.Property(x => x.ShippingCharge).HasPrecision(18, 2);
        entity.Property(x => x.ShippingTaxRatePercent).HasPrecision(5, 2);
        entity.Property(x => x.ShippingTaxAmount).HasPrecision(18, 2);
        entity.Property(x => x.MoneyOrderAmount).HasPrecision(18, 2);
        entity.Property(x => x.MoneyOrderFee).HasPrecision(18, 2);
        entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
        entity.Property(x => x.ShippingWeightKilograms).HasPrecision(9, 3);
        entity.Property(x => x.ParcelLengthCentimeters).HasPrecision(9, 2);
        entity.Property(x => x.ParcelWidthCentimeters).HasPrecision(9, 2);
        entity.Property(x => x.ParcelHeightCentimeters).HasPrecision(9, 2);
        entity.Property(x => x.ShippingRecipientName).HasMaxLength(100);
        entity.Property(x => x.ShippingAddress).HasMaxLength(300);
        entity.HasIndex(x => x.MoneyOrderId).IsUnique().HasFilter("[MoneyOrderId] IS NOT NULL");
        entity.Property(x => x.ResolutionReason).HasMaxLength(250);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.ResolvedAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasIndex(x => x.TransactionNumber).IsUnique();
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt }).IsDescending(false, true);
        entity.HasIndex(x => new { x.Status, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CounterTerminalRow>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ShippingRateRow>().WithMany().HasForeignKey(x => x.ShippingRateId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.ResolvedByStaffId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAudit(entity);
    }

    private static void ConfigureTransactionLines(EntityTypeBuilder<TransactionLineRow> entity)
    {
        entity.ToTable("TransactionLines", table =>
        {
            table.HasCheckConstraint("CK_TransactionLines_Amounts", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [LineTotal] >= 0 AND [TaxAmount] >= 0");
            table.HasCheckConstraint("CK_TransactionLines_LineNumber", "[LineNumber] >= 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.LineNumber).IsRequired();
        entity.Property(x => x.Sku).HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(x => x.ProductName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
        entity.Property(x => x.LineTotal).HasPrecision(18, 2);
        entity.Property(x => x.TaxCategory).HasMaxLength(60).IsRequired();
        entity.Property(x => x.TaxRatePercent).HasPrecision(5, 2);
        entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
        entity.HasIndex(x => new { x.CounterTransactionId, x.LineNumber }).IsUnique();
        entity.HasOne<CounterTransactionRow>().WithMany().HasForeignKey(x => x.CounterTransactionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<ProductRow>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<TaxCategoryRow>().WithMany().HasForeignKey(x => x.TaxCategory).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePayments(EntityTypeBuilder<PaymentRow> entity)
    {
        entity.ToTable("Payments", table => table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.PaidAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => x.CounterTransactionId).IsUnique().HasFilter("[CounterTransactionId] IS NOT NULL");
        entity.HasOne<CounterTransactionRow>().WithOne().HasForeignKey<PaymentRow>(x => x.CounterTransactionId).OnDelete(DeleteBehavior.Cascade);
        ConfigureAudit(entity);
    }

    private static void ConfigureAuditEntries(EntityTypeBuilder<AuditEntryRow> entity)
    {
        entity.ToTable("AuditEntries");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        entity.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        entity.Property(x => x.SafeDetailsJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.RouteValuesJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
        entity.HasIndex(x => x.CorrelationId);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureNotifications(EntityTypeBuilder<OperationalNotificationRow> entity)
    {
        entity.ToTable("OperationalNotifications");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Kind).HasMaxLength(60).IsUnicode(false).IsRequired();
        entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
        entity.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        entity.Property(x => x.ReferenceId).HasMaxLength(80).IsUnicode(false);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => new { x.BranchId, x.IsAcknowledged, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureOutbox(EntityTypeBuilder<OutboxMessageRow> entity)
    {
        entity.ToTable("OutboxMessages", table => table.HasCheckConstraint(
            "CK_OutboxMessages_Attempts", "[AttemptCount] >= 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.EventType).HasMaxLength(160).IsUnicode(false).IsRequired();
        entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.LastError).HasMaxLength(2000);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.ProcessedAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
    }

    private static void ConfigureAudit<TRow>(EntityTypeBuilder<TRow> entity)
        where TRow : class, IAuditedRow
    {
        entity.OwnsOne(row => row.Audit, audit =>
        {
            audit.Property(x => x.CreatedAtUtc)
                .HasColumnName("CreatedAtUtc")
                .HasColumnType("datetime2(3)")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            audit.Property(x => x.UpdatedAtUtc)
                .HasColumnName("UpdatedAtUtc")
                .HasColumnType("datetime2(3)");
            audit.Property(x => x.CreatedByStaffId).HasColumnName("CreatedByStaffId");
            audit.Property(x => x.UpdatedByStaffId).HasColumnName("UpdatedByStaffId");
            audit.Property(x => x.CorrelationId).HasColumnName("AuditCorrelationId").HasMaxLength(64).IsUnicode(false);
        });
        entity.Navigation(row => row.Audit).IsRequired();
    }
}
