using ApexPost.Application.Models;
using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

public sealed class InventoryPageModel
{
    public IReadOnlyList<Branch> Branches { get; init; } = [];
    public Guid? SelectedBranchId { get; init; }
    public IReadOnlyList<ProductInventorySummary> Products { get; init; } = [];
    public bool CanManage { get; init; }
}
