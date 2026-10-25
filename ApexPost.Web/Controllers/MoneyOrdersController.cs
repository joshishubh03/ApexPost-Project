using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Middleware;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class MoneyOrdersController(
    MoneyOrderService moneyOrderService,
    IBranchRepository branchRepository,
    MoneyOrderPolicy policy) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid? branchId, CancellationToken cancellationToken)
    {
        var isAdministrator = User.IsInRole(nameof(StaffRole.Administrator));
        var branches = await branchRepository.GetAllAsync(cancellationToken);
        Guid? selectedBranchId;

        if (isAdministrator)
        {
            selectedBranchId = branchId;
        }
        else if (Guid.TryParse(User.FindFirstValue("branch_id"), out var assignedBranchId))
        {
            selectedBranchId = assignedBranchId;
            branches = branches.Where(branch => branch.Id == assignedBranchId).ToArray();
        }
        else
        {
            return Forbid();
        }

        if (selectedBranchId is { } selected && branches.All(branch => branch.Id != selected))
        {
            return NotFound();
        }

        return View(new MoneyOrdersPageModel
        {
            Branches = branches,
            SelectedBranchId = selectedBranchId,
            Orders = selectedBranchId is { } id
                ? await moneyOrderService.GetForBranchAsync(id, cancellationToken)
                : [],
            IdentityVerificationThreshold = policy.IdentityVerificationThreshold,
            FeeAmount = policy.FeeAmount,
            CanIssue = CanIssueOrders(),
            CanRefund = User.IsInRole(nameof(StaffRole.BranchManager))
        });
    }

    [HttpPost]
    [Authorize(Roles = nameof(StaffRole.BranchManager))]
    public async Task<IActionResult> Refund(RefundMoneyOrderInputModel input, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue("branch_id"), out var branchId) ||
            !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var managerId))
            return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Enter a refund reason between 3 and 250 characters.";
            return RedirectToAction(nameof(Index), new { branchId });
        }

        try
        {
            var order = await moneyOrderService.RefundAsync(branchId, input.MoneyOrderId, managerId,
                input.Reason, CorrelationIdMiddleware.GetCorrelationId(HttpContext), cancellationToken);
            TempData["StatusMessage"] = $"Money order {order.MoneyOrderNumber} refunded for {order.TotalAmount:C2}.";
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { branchId });
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,BranchManager,CounterClerk")]
    public async Task<IActionResult> Issue(CreateMoneyOrderInputModel input, CancellationToken cancellationToken)
    {
        var isAdministrator = User.IsInRole(nameof(StaffRole.Administrator));
        Guid? branchId = isAdministrator
            ? input.BranchId
            : Guid.TryParse(User.FindFirstValue("branch_id"), out var assignedBranchId)
                ? assignedBranchId
                : null;

        if (branchId is null || !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Check the sender, recipient, amount, and any required identity details.";
            return RedirectToAction(nameof(Index), new { branchId });
        }

        try
        {
            var order = await moneyOrderService.IssueAsync(
                branchId.Value,
                staffId,
                input.SenderName,
                input.RecipientName,
                input.Amount,
                input.DocumentType,
                input.DocumentIdentifier,
                CorrelationIdMiddleware.GetCorrelationId(HttpContext),
                cancellationToken);
            TempData["StatusMessage"] = $"Money order {order.MoneyOrderNumber} issued. Total: {order.TotalAmount:C2}.";
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { branchId });
    }

    private bool CanIssueOrders() =>
        User.IsInRole(nameof(StaffRole.Administrator)) ||
        User.IsInRole(nameof(StaffRole.BranchManager)) ||
        User.IsInRole(nameof(StaffRole.CounterClerk));
}
