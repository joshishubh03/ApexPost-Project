using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Queries;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ReportsController(
    ReportingService reportingService,
    IBranchRepository branchRepository,
    IReportingQueries? reportingQueries = null) : Controller
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

    [HttpGet]
    [Authorize(Roles = nameof(StaffRole.Auditor))]
    public async Task<IActionResult> AuditOverview(CancellationToken cancellationToken)
    {
        var branches = (await branchRepository.GetAllAsync(cancellationToken))
            .OrderBy(branch => branch.Code)
            .ToArray();
        var reports = new List<BranchOperationsReport>(branches.Length);
        foreach (var branch in branches)
        {
            reports.Add(await reportingService.GetBranchReportAsync(branch.Id, cancellationToken));
        }

        return View(reports.ToArray());
    }

    /// <summary>
    /// SQL Server operational reports (query side): daily branch sales and low stock come from stored
    /// procedures; shipping volume, money orders/verifications and "no sales today" are AsNoTracking projections.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Administrator,BranchManager,Auditor")]
    public async Task<IActionResult> Operations(
        [FromQuery] OperationsReportQuery query,
        CancellationToken cancellationToken)
    {
        if (reportingQueries is null)
        {
            return View(new OperationsReportPageModel { IsAvailable = false });
        }

        var branches = (await branchRepository.GetAllAsync(cancellationToken)).OrderBy(branch => branch.Code).ToArray();
        if (!User.IsInRole(nameof(StaffRole.Administrator)) && !User.IsInRole(nameof(StaffRole.Auditor)))
        {
            if (!Guid.TryParse(User.FindFirstValue("branch_id"), out var assignedBranchId))
                return Forbid();
            branches = branches.Where(branch => branch.Id == assignedBranchId).ToArray();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var date = query.Date ?? today.AddDays(-1);
        var from = query.From ?? today.AddDays(-30);
        var to = query.To ?? today;
        if (from > to || date > today)
        {
            ModelState.AddModelError(string.Empty, "Choose a valid date range that does not end in the future.");
        }

        var selectedCode = branches.Any(branch => branch.Code == query.BranchCode)
            ? query.BranchCode
            : branches.FirstOrDefault()?.Code;
        var model = new OperationsReportPageModel
        {
            IsAvailable = true,
            Branches = branches,
            Query = new OperationsReportQuery { BranchCode = selectedCode, Date = date, From = from, To = to, Threshold = query.Threshold }
        };
        if (!ModelState.IsValid || selectedCode is null)
        {
            return View(model);
        }

        // Sequential awaits: the query service shares the request's DbContext.
        model.DailySales = await reportingQueries.GetDailyBranchSalesAsync(selectedCode, date, cancellationToken);
        model.LowStock = await reportingQueries.GetLowStockAsync(
            User.IsInRole(nameof(StaffRole.BranchManager)) ? selectedCode : query.AllBranches ? null : selectedCode,
            query.Threshold, cancellationToken);
        model.ShippingVolume = await reportingQueries.GetShippingVolumeAsync(from, to, cancellationToken);
        model.MoneyOrders = (await reportingQueries.GetMoneyOrdersByBranchAsync(from, to, cancellationToken))
            .Where(row => branches.Any(branch => branch.Code == row.BranchCode))
            .ToArray();
        model.BranchesWithoutSales = (await reportingQueries.GetBranchesWithoutSalesAsync(date, cancellationToken))
            .Where(row => branches.Any(branch => branch.Code == row.BranchCode))
            .ToArray();
        return View(model);
    }
}
