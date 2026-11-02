using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Conversions;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

// Delete behaviour: lines, payment and shipment belong to the CounterTransactions aggregate and CASCADE
// with it. Everything that points at history from outside the aggregate (money orders, audit entries,
// stock adjustments) is RESTRICT, so a financial record can never disappear silently.

public sealed class IdentityVerificationRowConfiguration : IEntityTypeConfiguration<IdentityVerificationRow>
{
    public void Configure(EntityTypeBuilder<IdentityVerificationRow> entity)
    {
        entity.ToTable("IdentityVerifications", table =>
        {
            // Masked storage only: varchar(16), at least four leading '*' and at most four visible characters.
            table.HasCheckConstraint("CK_IdentityVerifications_MaskedIdentifier",
                "LEN([MaskedDocumentIdentifier]) <= 16 AND [MaskedDocumentIdentifier] LIKE '****%' AND LEN(REPLACE([MaskedDocumentIdentifier], '*', '')) <= 4");
            table.HasCheckConstraint("CK_IdentityVerifications_Status",
                CheckConstraintSql.AllowedValues("Status", [nameof(IdentityVerificationStatus.Verified), nameof(IdentityVerificationStatus.Failed)]));
            table.HasCheckConstraint("CK_IdentityVerifications_DocumentType",
                CheckConstraintSql.AllowedValues<IdentityDocumentType>("DocumentType"));
            table.HasCheckConstraint("CK_IdentityVerifications_Amount", "[MoneyOrderAmount] > 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        entity.Property(x => x.MaskedDocumentIdentifier).HasMaxLength(IdentityDocumentMask.MaxLength).IsUnicode(false).IsRequired();
        entity.Property(x => x.MoneyOrderAmount).HasPrecision(18, 2);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.ConfigureAuditColumns();
    }
}

public sealed class MoneyOrderRowConfiguration : IEntityTypeConfiguration<MoneyOrderRow>
{
    public void Configure(EntityTypeBuilder<MoneyOrderRow> entity)
    {
        entity.ToTable("MoneyOrders", table =>
        {
            table.HasCheckConstraint("CK_MoneyOrders_Amounts", "[Amount] > 0 AND [Fee] >= 0");
            table.HasCheckConstraint("CK_MoneyOrders_MaskedIdentifier",
                "[MaskedDocumentIdentifier] IS NULL OR (LEN([MaskedDocumentIdentifier]) <= 16 AND [MaskedDocumentIdentifier] LIKE '****%' AND LEN(REPLACE([MaskedDocumentIdentifier], '*', '')) <= 4)");
            table.HasCheckConstraint("CK_MoneyOrders_Status", CheckConstraintSql.AllowedValues<MoneyOrderStatus>("Status"));
            table.HasCheckConstraint("CK_MoneyOrders_VerificationStatus",
                CheckConstraintSql.AllowedValues("VerificationStatus", [nameof(IdentityVerificationStatus.NotRequired), nameof(IdentityVerificationStatus.Verified)]));
            table.HasCheckConstraint("CK_MoneyOrders_DocumentType",
                CheckConstraintSql.AllowedValues<IdentityDocumentType>("DocumentType", allowNull: true));
            table.HasCheckConstraint("CK_MoneyOrders_Refund",
                "([Status] = 'Refunded' AND [RefundedAt] IS NOT NULL AND [RefundedByStaffId] IS NOT NULL AND [RefundReason] IS NOT NULL) OR ([Status] = 'Issued' AND [RefundedAt] IS NULL AND [RefundedByStaffId] IS NULL AND [RefundReason] IS NULL)");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.MoneyOrderNumber).HasMaxLength(40).IsUnicode(false).IsRequired();
        entity.Property(x => x.SenderName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.RecipientName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Fee).HasPrecision(18, 2);
        entity.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        entity.Property(x => x.MaskedDocumentIdentifier).HasMaxLength(IdentityDocumentMask.MaxLength).IsUnicode(false);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.RefundReason).HasMaxLength(250);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.RefundedAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => x.MoneyOrderNumber).IsUnique();
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
        // ADR 0042 / workload 4: filtered index for the auditors' search of identity-verified orders
        // (about a fifth of all orders) - 1,186 -> 17 logical reads, 904 KB.
        entity.HasIndex(x => x.OccurredAt, "IX_MoneyOrders_Verified_OccurredAt")
            .HasFilter("[VerificationStatus] = 'Verified'")
            .IncludeProperties(x => new { x.MoneyOrderNumber, x.Amount, x.MaskedDocumentIdentifier, x.BranchId, x.Status });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<IdentityVerificationRow>().WithMany().HasForeignKey(x => x.IdentityVerificationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CounterTransactionRow>().WithMany().HasForeignKey(x => x.CounterTransactionId).OnDelete(DeleteBehavior.Restrict);
        entity.ConfigureAuditColumns();
    }
}

public sealed class CounterTransactionRowConfiguration : IEntityTypeConfiguration<CounterTransactionRow>
{
    public void Configure(EntityTypeBuilder<CounterTransactionRow> entity)
    {
        entity.ToTable("CounterTransactions", table =>
        {
            table.HasCheckConstraint("CK_CounterTransactions_Totals", "[Subtotal] >= 0 AND [TaxTotal] >= 0 AND [ShippingCharge] >= 0 AND [TotalAmount] >= 0");
            table.HasCheckConstraint("CK_CounterTransactions_Reversal", "([Status] IN ('Voided','Refunded') AND [ResolvedAt] IS NOT NULL AND [ResolvedByStaffId] IS NOT NULL AND [ResolutionReason] IS NOT NULL) OR ([Status] NOT IN ('Voided','Refunded') AND [ResolvedAt] IS NULL AND [ResolvedByStaffId] IS NULL AND [ResolutionReason] IS NULL)");
            table.HasCheckConstraint("CK_CounterTransactions_Status", CheckConstraintSql.AllowedValues<TransactionStatus>("Status"));
            table.HasCheckConstraint("CK_CounterTransactions_MoneyOrder", "[MoneyOrderAmount] >= 0 AND [MoneyOrderFee] >= 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.TransactionNumber).HasMaxLength(40).IsUnicode(false).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.Subtotal).HasPrecision(18, 2);
        entity.Property(x => x.TaxTotal).HasPrecision(18, 2);
        entity.Property(x => x.ShippingCharge).HasPrecision(18, 2);
        entity.Property(x => x.ShippingTaxRatePercent).HasPrecision(5, 2);
        entity.Property(x => x.ShippingTaxAmount).HasPrecision(18, 2);
        entity.Property(x => x.MoneyOrderAmount).HasPrecision(18, 2);
        entity.Property(x => x.MoneyOrderFee).HasPrecision(18, 2);
        entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
        entity.HasIndex(x => x.MoneyOrderId).IsUnique().HasFilter("[MoneyOrderId] IS NOT NULL");
        entity.Property(x => x.ResolutionReason).HasMaxLength(250);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.ResolvedAt).HasColumnType("datetimeoffset(3)");
        // Optimistic concurrency for voids/refunds racing each other.
        entity.ConfigureRowVersion();
        entity.HasIndex(x => x.TransactionNumber).IsUnique();
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt }).IsDescending(false, true);
        entity.HasIndex(x => new { x.Status, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CounterTerminalRow>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.ResolvedByStaffId).OnDelete(DeleteBehavior.Restrict);

        // Aggregate parts: cascade with the transaction.
        entity.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.CounterTransactionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Payment).WithOne().HasForeignKey<PaymentRow>(x => x.CounterTransactionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Shipment).WithOne().HasForeignKey<ShipmentRow>(x => x.CounterTransactionId).OnDelete(DeleteBehavior.Cascade);
        entity.ConfigureAuditColumns();
    }
}

public sealed class TransactionLineRowConfiguration : IEntityTypeConfiguration<TransactionLineRow>
{
    public void Configure(EntityTypeBuilder<TransactionLineRow> entity)
    {
        entity.ToTable("TransactionLines", table =>
        {
            table.HasCheckConstraint("CK_TransactionLines_Amounts", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [LineTotal] >= 0 AND [TaxAmount] >= 0");
            table.HasCheckConstraint("CK_TransactionLines_LineNumber", "[LineNumber] >= 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.LineNumber).IsRequired();
        entity.Property(x => x.Sku).HasConversion(ValueObjectConverters.Sku).HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(x => x.ProductName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
        entity.Property(x => x.LineTotal).HasPrecision(18, 2);
        entity.Property(x => x.TaxCategory).HasMaxLength(60).IsRequired();
        entity.Property(x => x.TaxRatePercent).HasPrecision(5, 2);
        entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
        entity.HasIndex(x => new { x.CounterTransactionId, x.LineNumber }).IsUnique();
        entity.HasOne<ProductRow>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<TaxCategoryRow>().WithMany().HasForeignKey(x => x.TaxCategory).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentRowConfiguration : IEntityTypeConfiguration<PaymentRow>
{
    public void Configure(EntityTypeBuilder<PaymentRow> entity)
    {
        entity.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Payments_Method", CheckConstraintSql.AllowedValues<PaymentMethod>("Method"));
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.PaidAt).HasColumnType("datetimeoffset(3)");
        entity.HasIndex(x => x.CounterTransactionId).IsUnique().HasFilter("[CounterTransactionId] IS NOT NULL");
        entity.ConfigureAuditColumns();
    }
}

public sealed class ShipmentRowConfiguration : IEntityTypeConfiguration<ShipmentRow>
{
    public void Configure(EntityTypeBuilder<ShipmentRow> entity)
    {
        entity.ToTable("Shipments", table =>
        {
            table.HasCheckConstraint("CK_Shipments_Weight",
                $"[WeightKilograms] > 0 AND [WeightKilograms] <= {Weight.MaximumKilograms:0}");
            table.HasCheckConstraint("CK_Shipments_Dimensions",
                "([LengthCentimeters] IS NULL AND [WidthCentimeters] IS NULL AND [HeightCentimeters] IS NULL) OR ([LengthCentimeters] > 0 AND [WidthCentimeters] > 0 AND [HeightCentimeters] > 0)");
            table.HasCheckConstraint("CK_Shipments_Status", CheckConstraintSql.AllowedValues<ShipmentStatus>("Status"));
            table.HasCheckConstraint("CK_Shipments_ShipmentType",
                CheckConstraintSql.AllowedValues<ShipmentType>("ShipmentType", allowNull: true));
            table.HasCheckConstraint("CK_Shipments_LetterHasNoDimensions",
                "[ShipmentType] IS NULL OR [ShipmentType] <> 'Letter' OR [LengthCentimeters] IS NULL");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ShipmentType).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.Weight)
            .HasConversion(ValueObjectConverters.Weight)
            .HasColumnName("WeightKilograms")
            .HasPrecision(9, 3);
        entity.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .HasDefaultValue(ShipmentStatus.Booked);
        entity.OwnsOne(x => x.Dimensions, dimensions =>
        {
            dimensions.Property(x => x.LengthCentimeters).HasColumnName("LengthCentimeters").HasPrecision(9, 2);
            dimensions.Property(x => x.WidthCentimeters).HasColumnName("WidthCentimeters").HasPrecision(9, 2);
            dimensions.Property(x => x.HeightCentimeters).HasColumnName("HeightCentimeters").HasPrecision(9, 2);
        });
        entity.OwnsOne(x => x.Destination, destination =>
        {
            destination.Property(x => x.RecipientName).HasColumnName("RecipientName").HasMaxLength(100);
            destination.Property(x => x.AddressLine).HasColumnName("AddressLine").HasMaxLength(300);
        });
        entity.HasIndex(x => x.CounterTransactionId).IsUnique();
        // ADR 0042 / workload 3: same keys as before, now covering (INCLUDE) so the shipment search seeks instead of
        // scanning Shipments (2,808 -> 17 reads). It replaces the narrow index, so there is still one index to maintain.
        entity.HasIndex(x => new { x.ShippingRateId, x.Status })
            .IncludeProperties(x => new { x.CounterTransactionId, x.Weight });
        entity.HasOne<ShippingRateRow>().WithMany().HasForeignKey(x => x.ShippingRateId).OnDelete(DeleteBehavior.Restrict);
        entity.ConfigureAuditColumns();
    }
}

public sealed class AuditEntryRowConfiguration : IEntityTypeConfiguration<AuditEntryRow>
{
    public void Configure(EntityTypeBuilder<AuditEntryRow> entity)
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
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)").HasDefaultValueSql("SYSDATETIMEOFFSET()");
        entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
        entity.HasIndex(x => x.CorrelationId);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OperationalNotificationRowConfiguration : IEntityTypeConfiguration<OperationalNotificationRow>
{
    public void Configure(EntityTypeBuilder<OperationalNotificationRow> entity)
    {
        entity.ToTable("OperationalNotifications");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Kind).HasMaxLength(60).IsUnicode(false).IsRequired();
        entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
        entity.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        entity.Property(x => x.ReferenceId).HasMaxLength(80).IsUnicode(false);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.IsAcknowledged).HasDefaultValue(false);
        entity.HasIndex(x => new { x.BranchId, x.IsAcknowledged, x.OccurredAt });
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StaffRow>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OutboxMessageRowConfiguration : IEntityTypeConfiguration<OutboxMessageRow>
{
    public void Configure(EntityTypeBuilder<OutboxMessageRow> entity)
    {
        entity.ToTable("OutboxMessages", table => table.HasCheckConstraint(
            "CK_OutboxMessages_Attempts", "[AttemptCount] >= 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.EventType).HasMaxLength(160).IsUnicode(false).IsRequired();
        entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.LastError).HasMaxLength(2000);
        entity.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.ProcessedAt).HasColumnType("datetimeoffset(3)");
        entity.Property(x => x.AttemptCount).HasDefaultValue(0);
        entity.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
    }
}
