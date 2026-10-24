using ApexPost.Application.Models;
using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

public sealed class CounterTransactionPageModel
{
    public IReadOnlyList<Branch> Branches { get; init; } = [];
    public Guid? SelectedBranchId { get; init; }
    public CounterRegisterContext? Register { get; init; }
    public bool CanCreate { get; init; }
}
