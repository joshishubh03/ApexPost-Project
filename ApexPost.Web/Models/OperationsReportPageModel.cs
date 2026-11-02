using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Queries;
using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

/// <summary>Request DTO for the operations report filters (GET query string).</summary>
public sealed class OperationsReportQuery
{
    [StringLength(12)]
    public string? BranchCode { get; set; }

    public DateOnly? Date { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [Range(0, 1000)]
    public int Threshold { get; set; } = 10;

    public bool AllBranches { get; set; }
}

/// <summary>View model (response DTO) for the operations report screen.</summary>
public sealed class OperationsReportPageModel
{
    public bool IsAvailable { get; init; }
    public IReadOnlyList<Branch> Branches { get; init; } = [];
    public OperationsReportQuery Query { get; init; } = new();
    public IReadOnlyList<DailyBranchSalesDto> DailySales { get; set; } = [];
    public IReadOnlyList<LowStockDto> LowStock { get; set; } = [];
    public IReadOnlyList<ShippingVolumeDto> ShippingVolume { get; set; } = [];
    public IReadOnlyList<MoneyOrderBranchSummaryDto> MoneyOrders { get; set; } = [];
    public IReadOnlyList<BranchWithoutSalesDto> BranchesWithoutSales { get; set; } = [];
}
