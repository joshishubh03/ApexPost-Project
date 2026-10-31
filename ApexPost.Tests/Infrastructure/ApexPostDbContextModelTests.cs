using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Tests.Infrastructure;

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

        Assert.Contains("StaffRoles", tableNames);
        Assert.Contains("ProductCategories", tableNames);
        Assert.Contains("TaxCategories", tableNames);
        Assert.Contains("BranchStock", tableNames);
        Assert.Contains("StockAdjustments", tableNames);
        Assert.Contains("CounterTransactions", tableNames);
        Assert.Contains("TransactionLines", tableNames);
        Assert.Contains("Payments", tableNames);
        Assert.Contains("IdentityVerifications", tableNames);
        Assert.Contains("AuditEntries", tableNames);
        Assert.Contains("OutboxMessages", tableNames);
    }

    [Fact]
    public void Model_UsesCompositeStockKeyAndRowVersionConcurrencyTokens()
    {
        using var context = CreateContext();
        var stock = context.Model.FindEntityType(typeof(BranchStockRow));
        var stockKey = Assert.Single(stock!.GetKeys(), key => key.IsPrimaryKey());

        Assert.Equal(
            new[] { nameof(BranchStockRow.BranchId), nameof(BranchStockRow.ProductId) },
            stockKey.Properties.Select(property => property.Name));
        Assert.True(stock.FindProperty(nameof(BranchStockRow.RowVersion))!.IsConcurrencyToken);

        var transaction = context.Model.FindEntityType(typeof(CounterTransactionRow));
        Assert.True(transaction!.FindProperty(nameof(CounterTransactionRow.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void IdentityVerificationSchema_StoresOnlyMaskedDocumentIdentifiers()
    {
        using var context = CreateContext();
        var verification = context.Model.FindEntityType(typeof(IdentityVerificationRow));
        var propertyNames = verification!.GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains(nameof(IdentityVerificationRow.MaskedDocumentIdentifier), propertyNames);
        Assert.DoesNotContain(propertyNames, name => name.Contains("FullDocument", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("DocumentNumber", StringComparison.OrdinalIgnoreCase));
    }
}
