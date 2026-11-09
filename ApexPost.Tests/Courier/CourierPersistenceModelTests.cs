using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ApexPost.Tests.Courier;

/// <summary>Week 4 tables in the EF model (no database connection is opened).</summary>
public sealed class CourierPersistenceModelTests
{
    private static ApexPostDbContext CreateContext() => new(new DbContextOptionsBuilder<ApexPostDbContext>()
        .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ApexPostModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
        .Options);

    [Fact]
    public void Migrations_and_snapshot_cover_the_whole_model()
    {
        using var context = CreateContext();

        // Fails if a model change (for example the Week 4 courier tables) has no matching migration/snapshot.
        Assert.False(context.Database.HasPendingModelChanges(),
            "The EF model differs from the migration snapshot. Run: dotnet ef migrations add <Name> --project ApexPost.Infrastructure --startup-project ApexPost.Web");
        Assert.Contains("20261109090000_Week4CourierShipmentsApi", context.Database.GetMigrations());
    }

    [Fact]
    public void Courier_tables_have_the_unique_tracking_number_and_check_constraints()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var shipments = model.FindEntityType(typeof(CourierShipmentRow))!;
        var scans = model.FindEntityType(typeof(ShipmentScanRow))!;

        Assert.Equal("CourierShipments", shipments.GetTableName());
        Assert.Equal("ShipmentScans", scans.GetTableName());
        Assert.Contains(shipments.GetIndexes(), index => index.IsUnique &&
            index.Properties.Single().Name == nameof(CourierShipmentRow.TrackingNumber));
        Assert.Equal(
            new[] { "CK_CourierShipments_Status", "CK_CourierShipments_TrackingNumber", "CK_CourierShipments_Weight" },
            shipments.GetCheckConstraints().Select(check => check.Name!).Order().ToArray());
        Assert.Contains(scans.GetCheckConstraints(), check => check.Name == "CK_ShipmentScans_DeliveredSignature");
        Assert.True(shipments.FindProperty(nameof(CourierShipmentRow.RowVersion))!.IsConcurrencyToken);
    }
}
