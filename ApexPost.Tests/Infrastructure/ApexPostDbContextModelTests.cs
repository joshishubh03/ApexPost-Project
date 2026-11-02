using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ApexPost.Tests.Infrastructure;

/// <summary>Model-only tests (no database connection is opened).</summary>
public sealed class ApexPostDbContextModelTests
{
    private static ApexPostDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ApexPostModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new ApexPostDbContext(options);
    }

    [Fact]
    public void Model_UsesNormalizedCoreTablesAndSeparateIdentityVerification()
    {
        using var context = CreateContext();
        var tableNames = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var table in new[]
                 {
                     "Branches", "CounterTerminals", "Staff", "StaffRoles", "Products", "ProductCategories", "BranchStock",
                     "StockAdjustments", "ShippingZones", "ShippingRates", "Shipments", "MoneyOrders", "IdentityVerifications",
                     "CounterTransactions", "TransactionLines", "Payments", "AuditEntries", "TaxCategories", "OutboxMessages"
                 })
        {
            Assert.Contains(table, tableNames);
        }
    }

    [Fact]
    public void Model_UsesCompositeStockKeyAndShadowRowVersionConcurrencyTokens()
    {
        using var context = CreateContext();
        var stock = context.Model.FindEntityType(typeof(BranchStockRow))!;
        var stockKey = Assert.Single(stock.GetKeys(), key => key.IsPrimaryKey());
        Assert.Equal(
            new[] { nameof(BranchStockRow.BranchId), nameof(BranchStockRow.ProductId) },
            stockKey.Properties.Select(property => property.Name));

        foreach (var type in new[] { typeof(BranchStockRow), typeof(CounterTransactionRow), typeof(ProductRow) })
        {
            var rowVersion = context.Model.FindEntityType(type)!.FindProperty(AuditShadowProperties.RowVersion)!;
            Assert.True(rowVersion.IsShadowProperty());
            Assert.True(rowVersion.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        }
    }

    [Fact]
    public void Audit_columns_are_shadow_properties_with_a_utc_default()
    {
        using var context = CreateContext();
        foreach (var entity in context.Model.GetEntityTypes().Where(entity => typeof(IAuditedRow).IsAssignableFrom(entity.ClrType) && !entity.IsOwned()))
        {
            var created = entity.FindProperty(AuditShadowProperties.CreatedAtUtc);
            Assert.NotNull(created);
            Assert.True(created.IsShadowProperty(), $"{entity.ClrType.Name}.CreatedAtUtc should be a shadow property");
            Assert.Equal("SYSUTCDATETIME()", created.GetDefaultValueSql());
            Assert.True(entity.FindProperty(AuditShadowProperties.CreatedBy)!.IsShadowProperty());
            Assert.True(entity.FindProperty(AuditShadowProperties.ModifiedAtUtc)!.IsShadowProperty());
        }
    }

    [Fact]
    public void Value_objects_use_value_converters_and_owned_types_map_to_shipment_columns()
    {
        using var context = CreateContext();
        Assert.NotNull(context.Model.FindEntityType(typeof(ProductRow))!.FindProperty(nameof(ProductRow.Sku))!.GetValueConverter());
        Assert.NotNull(context.Model.FindEntityType(typeof(BranchRow))!.FindProperty(nameof(BranchRow.Code))!.GetValueConverter());
        Assert.NotNull(context.Model.FindEntityType(typeof(ShippingRateRow))!.FindProperty(nameof(ShippingRateRow.BasePrice))!.GetValueConverter());
        Assert.NotNull(context.Model.FindEntityType(typeof(ShipmentRow))!.FindProperty(nameof(ShipmentRow.Weight))!.GetValueConverter());

        var dimensions = context.Model.FindEntityType(typeof(ParcelDimensions))!;
        Assert.True(dimensions.IsOwned());
        Assert.Equal("Shipments", dimensions.GetTableName());
        Assert.True(context.Model.FindEntityType(typeof(ShippingAddress))!.IsOwned());
    }

    [Fact]
    public void Every_entity_has_its_own_configuration_class()
    {
        var configurationTypes = typeof(ApexPostDbContext).Assembly.GetTypes()
            .Where(type => type.Namespace == "ApexPost.Infrastructure.Persistence.Configurations")
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToHashSet();

        using var context = CreateContext();
        foreach (var entity in context.Model.GetEntityTypes().Where(entity => !entity.IsOwned()))
        {
            Assert.Contains(entity.ClrType, configurationTypes);
        }
    }

    [Fact]
    public void Identity_verification_schema_stores_only_masked_document_identifiers()
    {
        using var context = CreateContext();
        // Check constraints live only in the design-time model.
        var verification = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(IdentityVerificationRow))!;
        var propertyNames = verification.GetProperties().Select(property => property.Name).ToArray();
        var masked = verification.FindProperty(nameof(IdentityVerificationRow.MaskedDocumentIdentifier))!;

        Assert.Equal(IdentityDocumentMask.MaxLength, masked.GetMaxLength());
        Assert.False(masked.IsUnicode());
        Assert.DoesNotContain(propertyNames, name => name.Contains("FullDocument", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("DocumentNumber", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(verification.GetCheckConstraints(), check => check.Name == "CK_IdentityVerifications_MaskedIdentifier");
    }

    [Fact]
    public void Status_and_weight_columns_have_check_constraints()
    {
        using var context = CreateContext();
        var checks = context.GetService<IDesignTimeModel>().Model.GetEntityTypes().SelectMany(entity => entity.GetCheckConstraints()).Select(check => check.Name).ToHashSet();

        Assert.Contains("CK_CounterTransactions_Status", checks);
        Assert.Contains("CK_MoneyOrders_Status", checks);
        Assert.Contains("CK_Payments_Method", checks);
        Assert.Contains("CK_Shipments_Weight", checks);
        Assert.Contains("CK_BranchStock_QuantityOnHand", checks);
        Assert.Contains("CK_Payments_Amount", checks);
    }

    [Fact]
    public void ProductCatalogueVersionLookup_OrdersByIndexedRowVersionAndTakesOneRow()
    {
        using var context = CreateContext();
        var sql = context.Products.AsNoTracking()
            .OrderByDescending(row => EF.Property<byte[]>(row, AuditShadowProperties.RowVersion))
            .Select(row => EF.Property<byte[]>(row, AuditShadowProperties.RowVersion))
            .Take(1)
            .ToQueryString();

        Assert.Contains("TOP(@p)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY [p].[RowVersion] DESC", sql, StringComparison.OrdinalIgnoreCase);
    }
}
