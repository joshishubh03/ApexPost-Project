using ApexPost.Domain.Entities;

namespace ApexPost.Application.Models;

public sealed record ShippingVolumeSummary(string ZoneName, string ServiceLevel, int Shipments);

public sealed record BranchOperationsReport(
    Branch Branch,
    int CompletedSalesCount,
    decimal CompletedSalesTotal,
    int VoidedTransactionsCount,
    int RefundedTransactionsCount,
    IReadOnlyList<ProductInventorySummary> LowStockItems,
    IReadOnlyList<ShippingVolumeSummary> ShippingVolumes,
    int MoneyOrderCount,
    decimal MoneyOrderTotal,
    int RefundedMoneyOrderCount,
    int IdentityVerificationFailures,
    IReadOnlyList<AuditRecord> RecentAuditEvents);
