using ApexPost.Application.Models;
using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

public sealed class BranchReportPageModel
{
    public IReadOnlyList<Branch> Branches { get; init; } = [];
    public Guid? SelectedBranchId { get; init; }
    public BranchOperationsReport? Report { get; init; }
}
