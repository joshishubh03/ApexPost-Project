using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApexPost.Web.Models;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class HomeController(
    IBranchRepository branchRepository,
    ReportingService reportingService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Staff";
        var isAdministrator = User.IsInRole("Administrator");
        IReadOnlyList<BranchOperationsReport> reports = [];
        var branchName = User.FindFirstValue("branch_name") ?? "Administration";

        if (isAdministrator)
        {
            var branches = (await branchRepository.GetAllAsync(cancellationToken))
                .OrderBy(branch => branch.Code)
                .ToArray();
            reports = await Task.WhenAll(branches.Select(branch =>
                reportingService.GetBranchReportAsync(branch.Id, cancellationToken)));
        }
        else if (Guid.TryParse(User.FindFirstValue("branch_id"), out var branchId))
        {
            var report = await reportingService.GetBranchReportAsync(branchId, cancellationToken);
            reports = [report];
            branchName = report.Branch.Name;
        }

        return View(new HomeDashboardViewModel(branchName, role, isAdministrator, reports));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
