namespace ApexPost.Application.Models;

/// <summary>Result row of dbo.usp_DailyBranchSales (column names match the procedure's result set).</summary>
public sealed record DailyBranchSalesRow(
    string BranchCode,
    DateOnly SaleDate,
    int TransactionCount,
    decimal RetailRevenue,
    decimal ServiceRevenue);

/// <summary>Result row of dbo.usp_LowStockReport.</summary>
public sealed record LowStockReportRow(
    string BranchCode,
    string Sku,
    string ProductName,
    int QuantityOnHand,
    int ReorderLevel,
    int Shortfall);

/// <summary>Result row of dbo.usp_AdjustStock (the new quantity and the history row it wrote).</summary>
public sealed record StockAdjustmentResult(
    Guid StockAdjustmentId,
    int QuantityOnHand);
