namespace ApexPost.Application.Queries;

/// <summary>Read-model DTOs returned by query services. They are projections, never tracked entities.</summary>
public sealed record DailyBranchSalesDto(
    string BranchCode,
    DateOnly SaleDate,
    int TransactionCount,
    decimal RetailRevenue,
    decimal ServiceRevenue)
{
    public decimal TotalRevenue => RetailRevenue + ServiceRevenue;
}

public sealed record LowStockDto(
    string BranchCode,
    string Sku,
    string ProductName,
    int QuantityOnHand,
    int ReorderLevel,
    int Shortfall);

public sealed record ShippingVolumeDto(
    string ZoneCode,
    string ZoneName,
    string ServiceLevel,
    int ShipmentCount,
    decimal ShippingRevenue);

public sealed record MoneyOrderBranchSummaryDto(
    string BranchCode,
    string BranchName,
    int MoneyOrdersIssued,
    int MoneyOrdersRefunded,
    decimal MoneyOrderValue,
    int VerificationsPassed,
    int VerificationsFailed);

public sealed record BranchWithoutSalesDto(string BranchCode, string BranchName, string Region);

public sealed record DashboardSummaryDto(
    int BranchCount,
    int ActiveProductCount,
    int TransactionsToday,
    decimal RevenueToday,
    int LowStockItems,
    int MoneyOrdersToday);

public sealed record GeneratedSqlSample(string Name, string Sql);
