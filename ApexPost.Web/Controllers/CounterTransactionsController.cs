using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class CounterTransactionsController(
    CounterTransactionService transactionService,
    IBranchRepository branchRepository) : Controller
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

        if (selectedBranchId is { } requestedBranch && branches.All(branch => branch.Id != requestedBranch))
        {
            return NotFound();
        }

        var register = selectedBranchId is { } selected
            ? await transactionService.GetRegisterContextAsync(selected, cancellationToken)
            : null;

        return View(new CounterTransactionPageModel
        {
            Branches = branches,
            SelectedBranchId = selectedBranchId,
            Register = register,
            CanCreate = CanCreateTransactions()
        });
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,BranchManager,CounterClerk")]
    public async Task<IActionResult> Complete(
        CreateCounterTransactionInputModel input,
        CancellationToken cancellationToken)
    {
        var isAdministrator = User.IsInRole(nameof(StaffRole.Administrator));
        Guid? branchId = isAdministrator
            ? input.BranchId
            : Guid.TryParse(User.FindFirstValue("branch_id"), out var assignedBranchId)
                ? assignedBranchId
                : null;
        var staffIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (branchId is null || !Guid.TryParse(staffIdValue, out var staffId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Check the quantities and shipping weight, then try again.";
            return RedirectToAction(nameof(Index), new { branchId });
        }

        try
        {
            var transaction = await transactionService.CompleteSaleAsync(
                branchId.Value,
                staffId,
                input.Quantities,
                input.PaymentMethod,
                input.ShippingRateId,
                input.ShippingWeightKilograms,
                CorrelationIdMiddleware.GetCorrelationId(HttpContext),
                cancellationToken);
            TempData["StatusMessage"] = $"Transaction {transaction.TransactionNumber} completed. Total: {transaction.TotalAmount:C2}.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (KeyNotFoundException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { branchId });
    }

    private bool CanCreateTransactions() =>
        User.IsInRole(nameof(StaffRole.Administrator)) ||
        User.IsInRole(nameof(StaffRole.BranchManager)) ||
        User.IsInRole(nameof(StaffRole.CounterClerk));
}
