using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Conversions;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

// Delete behaviour: master data is never cascade-deleted. Branches, products, staff, zones and rates are
// referenced by financial history, so every FK to them is RESTRICT (deactivate instead of delete).

public sealed class BranchRowConfiguration : IEntityTypeConfiguration<BranchRow>
{
    public void Configure(EntityTypeBuilder<BranchRow> entity)
    {
        entity.ToTable("Branches", table => table.HasCheckConstraint(
            "CK_Branches_OpeningHours", "[OpeningTime] < [ClosingTime]"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code)
            .HasConversion(ValueObjectConverters.BranchCode)
            .HasMaxLength(BranchCode.MaxLength)
            .IsUnicode(false)
            .IsRequired();
        entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Region).HasMaxLength(80).IsRequired();
        entity.Property(x => x.OpeningTime).HasColumnType("time(0)");
        entity.Property(x => x.ClosingTime).HasColumnType("time(0)");
        entity.HasIndex(x => x.Code).IsUnique();
        entity.HasMany(x => x.Terminals).WithOne().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(x => x.Staff).WithOne().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.ConfigureAuditColumns();
    }
}

public sealed class CounterTerminalRowConfiguration : IEntityTypeConfiguration<CounterTerminalRow>
{
    public void Configure(EntityTypeBuilder<CounterTerminalRow> entity)
    {
        entity.ToTable("CounterTerminals");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(12).IsUnicode(false).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        entity.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
        entity.ConfigureAuditColumns();
    }
}

public sealed class StaffRowConfiguration : IEntityTypeConfiguration<StaffRow>
{
    public void Configure(EntityTypeBuilder<StaffRow> entity)
    {
        entity.ToTable("Staff");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Email).HasMaxLength(254).IsUnicode(false).IsRequired();
        entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(x => x.PasswordHash).HasMaxLength(512).IsUnicode(false).IsRequired();
        entity.HasIndex(x => x.Email).IsUnique();
        entity.HasIndex(x => new { x.BranchId, x.Role, x.IsActive });
        entity.HasOne<StaffRoleRow>().WithMany().HasForeignKey(x => x.Role).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        entity.ConfigureAuditColumns();
    }
}

public sealed class StaffRoleRowConfiguration : IEntityTypeConfiguration<StaffRoleRow>
{
    public void Configure(EntityTypeBuilder<StaffRoleRow> entity)
    {
        entity.ToTable("StaffRoles", table => table.HasCheckConstraint(
            "CK_StaffRoles_Code", CheckConstraintSql.AllowedValues<StaffRole>("Code")));
        entity.HasKey(x => x.Code);
        entity.Property(x => x.Code).HasMaxLength(32).IsUnicode(false);
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        entity.ConfigureAuditColumns();
    }
}

public sealed class ProductRowConfiguration : IEntityTypeConfiguration<ProductRow>
{
    public void Configure(EntityTypeBuilder<ProductRow> entity)
    {
        entity.ToTable("Products", table => table.HasCheckConstraint("CK_Products_Price", "[Price] >= 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Sku)
            .HasConversion(ValueObjectConverters.Sku)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
        entity.Property(x => x.Category).HasMaxLength(60).IsRequired();
        entity.Property(x => x.TaxCategory).HasMaxLength(60).IsRequired();
        entity.Property(x => x.Price).HasPrecision(18, 2);
        entity.ConfigureRowVersion();
        entity.HasIndex(x => x.Sku).IsUnique();
        entity.HasIndex(AuditShadowProperties.RowVersion);
        entity.HasOne<ProductCategoryRow>().WithMany().HasForeignKey(x => x.Category).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<TaxCategoryRow>().WithMany().HasForeignKey(x => x.TaxCategory).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        entity.ConfigureAuditColumns();
    }
}

public sealed class ProductCategoryRowConfiguration : IEntityTypeConfiguration<ProductCategoryRow>
{
    public void Configure(EntityTypeBuilder<ProductCategoryRow> entity)
    {
        entity.ToTable("ProductCategories");
        entity.HasKey(x => x.Code);
        entity.Property(x => x.Code).HasMaxLength(60);
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        entity.ConfigureAuditColumns();
    }
}

public sealed class TaxCategoryRowConfiguration : IEntityTypeConfiguration<TaxCategoryRow>
{
    public void Configure(EntityTypeBuilder<TaxCategoryRow> entity)
    {
        entity.ToTable("TaxCategories");
        entity.HasKey(x => x.Code);
        entity.Property(x => x.Code).HasMaxLength(60);
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        entity.ConfigureAuditColumns();
    }
}

public sealed class BranchStockRowConfiguration : IEntityTypeConfiguration<BranchStockRow>
{
    public void Configure(EntityTypeBuilder<BranchStockRow> entity)
    {
        entity.ToTable("BranchStock", table =>
        {
            table.HasCheckConstraint("CK_BranchStock_QuantityOnHand", "[QuantityOnHand] >= 0");
            table.HasCheckConstraint("CK_BranchStock_ReorderLevel", "[ReorderLevel] >= 0");
        });
        entity.HasKey(x => new { x.BranchId, x.ProductId });
        // Optimistic concurrency: two clerks selling the last unit cannot both commit.
        entity.ConfigureRowVersion();
        entity.HasOne<BranchRow>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ProductRow>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.BranchId, x.QuantityOnHand });
        // ADR 0042 / PERFORMANCE.md workload 1: head-office low-stock report (all branches) seeks on the range
        // predicate instead of scanning the clustered index (910 -> 30 logical reads at 60k stock rows).
        entity.HasIndex(x => x.QuantityOnHand, "IX_BranchStock_QuantityOnHand").IncludeProperties(x => x.ReorderLevel);
        entity.ConfigureAuditColumns();
    }
}

public sealed class StockAdjustmentRowConfiguration : IEntityTypeConfiguration<StockAdjustmentRow>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentRow> entity)
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
        entity.ConfigureAuditColumns();
    }
}

public sealed class ShippingZoneRowConfiguration : IEntityTypeConfiguration<ShippingZoneRow>
{
    public void Configure(EntityTypeBuilder<ShippingZoneRow> entity)
    {
        entity.ToTable("ShippingZones");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        entity.Property(x => x.Destination).HasMaxLength(120).IsRequired();
        entity.HasIndex(x => x.Code).IsUnique();
        entity.ConfigureAuditColumns();
    }
}

public sealed class ShippingRateRowConfiguration : IEntityTypeConfiguration<ShippingRateRow>
{
    public void Configure(EntityTypeBuilder<ShippingRateRow> entity)
    {
        entity.ToTable("ShippingRates", table =>
        {
            table.HasCheckConstraint("CK_ShippingRates_Prices", "[BasePrice] >= 0 AND [PricePerKilogram] >= 0");
            table.HasCheckConstraint("CK_ShippingRates_MaximumWeight",
                $"[MaximumWeightKilograms] > 0 AND [MaximumWeightKilograms] <= {Weight.MaximumKilograms:0}");
            table.HasCheckConstraint("CK_ShippingRates_ServiceLevel",
                CheckConstraintSql.AllowedValues<ShippingServiceLevel>("ServiceLevel"));
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ServiceLevel).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        entity.Property(x => x.BasePrice).HasConversion(ValueObjectConverters.Money).HasPrecision(18, 2);
        entity.Property(x => x.PricePerKilogram).HasConversion(ValueObjectConverters.Money).HasPrecision(18, 2);
        entity.Property(x => x.MaximumWeight)
            .HasConversion(ValueObjectConverters.Weight)
            .HasColumnName("MaximumWeightKilograms")
            .HasPrecision(9, 3);
        entity.HasIndex(x => new { x.ZoneId, x.ServiceLevel }).IsUnique();
        entity.HasOne<ShippingZoneRow>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        entity.ConfigureAuditColumns();
    }
}
