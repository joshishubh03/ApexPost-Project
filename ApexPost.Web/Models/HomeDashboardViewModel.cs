using ApexPost.Application.Models;

namespace ApexPost.Web.Models;

public sealed record HomeDashboardViewModel(
    string BranchName,
    string StaffRole,
    bool IsAdministrator,
    IReadOnlyList<BranchOperationsReport> Reports);
