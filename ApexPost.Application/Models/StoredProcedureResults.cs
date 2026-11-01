namespace ApexPost.Application.Models;

public sealed record DailyBranchSalesRow(
    string BranchCode,
    DateOnly SaleDate,
    decimal? ServiceRevenue,
    decimal? RetailRevenue);

public sealed record LowStockReportRow(
    string BranchCode,
    string Sku,
    string ProductName,
    int QuantityOnHand,
    int ReorderLevel,
    int BelowReorderLevel);
