using ApexPost.Domain.Courier;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

public sealed class CourierShipmentRowConfiguration : IEntityTypeConfiguration<CourierShipmentRow>
{
    public const string TrackingNumberCheck = "[TrackingNumber] LIKE 'APX-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'";
    public const string WeightCheck = "[WeightKilograms] >= 0.1 AND [WeightKilograms] <= 70";

    public void Configure(EntityTypeBuilder<CourierShipmentRow> entity)
    {
        entity.ToTable("CourierShipments", table =>
        {
            table.HasCheckConstraint("CK_CourierShipments_Status", CheckConstraintSql.AllowedValues<CourierShipmentStatus>("Status"));
            table.HasCheckConstraint("CK_CourierShipments_TrackingNumber", TrackingNumberCheck);
            table.HasCheckConstraint("CK_CourierShipments_Weight", WeightCheck);
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.TrackingNumber).HasMaxLength(12).IsUnicode(false).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false).IsRequired();
        entity.Property(x => x.WeightKilograms).HasPrecision(6, 2);
        entity.Property(x => x.RecipientName).HasMaxLength(Recipient.MaxNameLength).IsRequired();
        entity.Property(x => x.RecipientPhone).HasMaxLength(Recipient.MaxPhoneLength).IsUnicode(false).IsRequired();
        entity.Property(x => x.RecipientAddress).HasMaxLength(Recipient.MaxAddressLength).IsRequired();
        entity.Property(x => x.RecipientCity).HasMaxLength(Recipient.MaxCityLength).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
        entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(3)");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.TrackingNumber).IsUnique();
        // Paged list filtered by status and/or city.
        entity.HasIndex(x => new { x.Status, x.RecipientCity });
        entity.HasMany(x => x.Scans).WithOne().HasForeignKey(x => x.CourierShipmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ShipmentScanRowConfiguration : IEntityTypeConfiguration<ShipmentScanRow>
{
    public const string DeliveredSignatureCheck = "[ScanType] <> 'Delivered' OR [SignatureName] IS NOT NULL";

    public void Configure(EntityTypeBuilder<ShipmentScanRow> entity)
    {
        entity.ToTable("ShipmentScans", table =>
        {
            table.HasCheckConstraint("CK_ShipmentScans_ScanType", CheckConstraintSql.AllowedValues<CourierScanType>("ScanType"));
            table.HasCheckConstraint("CK_ShipmentScans_DeliveredSignature", DeliveredSignatureCheck);
        });
        // The primary key is the device scan GUID, so a re-uploaded scan can never be stored twice.
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ScanType).HasConversion<string>().HasMaxLength(16).IsUnicode(false).IsRequired();
        entity.Property(x => x.CourierId).HasMaxLength(40).IsUnicode(false).IsRequired();
        entity.Property(x => x.Location).HasMaxLength(120).IsRequired();
        entity.Property(x => x.ScannedAtUtc).HasColumnType("datetime2(3)");
        entity.Property(x => x.ReceivedAtUtc).HasColumnType("datetime2(3)");
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.SignatureName).HasMaxLength(120);
        // Scan history and "latest scan" per shipment (also serves the foreign key).
        entity.HasIndex(x => new { x.CourierShipmentId, x.ScannedAtUtc });
    }
}
