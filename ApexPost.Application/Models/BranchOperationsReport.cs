using ApexPost.Domain.Entities;

namespace ApexPost.Application.Models;

public sealed record ShippingVolumeSummary(string ZoneName, string ServiceLevel, int Shipments);

public sealed record DailySalesSummary(
    DateOnly Date,
    int CompletedTransactionCount,
    int StandaloneMoneyOrderCount,
    decimal RetailSales,
    decimal ServiceSales,
    decimal TotalSales);

public sealed record BranchOperationsReport(
    Branch Branch,
    int CompletedSalesCount,
    decimal CompletedSalesTotal,
    IReadOnlyList<DailySalesSummary> DailySales,
    int VoidedTransactionsCount,
    int RefundedTransactionsCount,
    IReadOnlyList<ProductInventorySummary> LowStockItems,
    IReadOnlyList<ShippingVolumeSummary> ShippingVolumes,
    int MoneyOrderCount,
    decimal MoneyOrderTotal,
    int RefundedMoneyOrderCount,
    int IdentityVerificationFailures,
    IReadOnlyList<OperationalNotification> RecentNotifications,
    IReadOnlyList<AuditRecord> RecentAuditEvents);
