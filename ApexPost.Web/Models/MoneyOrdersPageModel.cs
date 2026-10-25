using ApexPost.Application.Models;
using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

public sealed class MoneyOrdersPageModel
{
    public IReadOnlyList<Branch> Branches { get; init; } = [];
    public Guid? SelectedBranchId { get; init; }
    public IReadOnlyList<MoneyOrder> Orders { get; init; } = [];
    public decimal IdentityVerificationThreshold { get; init; }
    public decimal FeeAmount { get; init; }
    public bool CanIssue { get; init; }
    public bool CanRefund { get; init; }
    public IReadOnlySet<Guid> MoneyOrdersInCounterTransactions { get; init; } = new HashSet<Guid>();
}
