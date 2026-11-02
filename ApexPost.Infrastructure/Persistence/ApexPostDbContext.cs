using ApexPost.Infrastructure.Persistence.Configurations;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>
/// Relational write model for ApexPostDb. Mapping lives in one IEntityTypeConfiguration class per entity
/// (Configurations/*). Domain records are mapped to and from these rows at the repository boundary.
/// Registered as a scoped service: one DbContext per request, shared by repositories and the unit of work.
/// </summary>
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
    public DbSet<ShipmentRow> Shipments => Set<ShipmentRow>();
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
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApexPostDbContext).Assembly,
            type => type.Namespace == typeof(BranchRowConfiguration).Namespace);
        base.OnModelCreating(modelBuilder);
    }
}
