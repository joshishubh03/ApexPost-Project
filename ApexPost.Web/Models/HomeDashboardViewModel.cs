using ApexPost.Application.Models;
using ApexPost.Application.Queries;

namespace ApexPost.Web.Models;

public sealed record HomeDashboardViewModel(
    string BranchName,
    string StaffRole,
    bool IsAdministrator,
    IReadOnlyList<BranchOperationsReport> Reports,
    DashboardSummaryDto? Summary = null);
