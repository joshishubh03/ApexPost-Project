using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ReportsController(
    ReportingService reportingService,
    IBranchRepository branchRepository) : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,BranchManager,Auditor")]
    public async Task<IActionResult> Index(Guid? branchId, CancellationToken cancellationToken)
    {
        var branches = await branchRepository.GetAllAsync(cancellationToken);
        Guid? selectedBranchId;
        if (User.IsInRole(nameof(StaffRole.Administrator)))
        {
            selectedBranchId = branchId;
        }
        else if (Guid.TryParse(User.FindFirstValue("branch_id"), out var assignedBranchId))
        {
            branches = branches.Where(branch => branch.Id == assignedBranchId).ToArray();
            selectedBranchId = assignedBranchId;
        }
        else
        {
            return Forbid();
        }

        if (selectedBranchId is { } requestedBranch && branches.All(branch => branch.Id != requestedBranch))
        {
            return NotFound();
        }

        var report = selectedBranchId is { } selected
            ? await reportingService.GetBranchReportAsync(selected, cancellationToken)
            : null;

        return View("~/Views/BranchReport.cshtml", new BranchReportPageModel
        {
            Branches = branches,
            SelectedBranchId = selectedBranchId,
            Report = report
        });
    }
}
