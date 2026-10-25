using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class InventoryController(
    InventoryManagementService inventoryService,
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

        var products = selectedBranchId is { } selected
            ? await inventoryService.GetBranchInventoryAsync(selected, cancellationToken)
            : Array.Empty<ProductInventorySummary>();

        return View(new InventoryPageModel
        {
            Branches = branches,
            SelectedBranchId = selectedBranchId,
            Products = products,
            CanManage = CanManageInventory()
        });
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,BranchManager")]
    public async Task<IActionResult> Adjust(StockAdjustmentInputModel input, CancellationToken cancellationToken)
    {
        var isAdministrator = User.IsInRole(nameof(StaffRole.Administrator));
        Guid? branchId = isAdministrator
            ? input.BranchId
            : Guid.TryParse(User.FindFirstValue("branch_id"), out var assignedBranchId)
                ? assignedBranchId
                : null;
        var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (branchId is null || !Guid.TryParse(staffId, out var currentStaffId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Enter a non-zero quantity change, a non-negative reorder level, and a reason.";
            return RedirectToAction(nameof(Index), new { branchId });
        }

        try
        {
            await inventoryService.ChangeStockAsync(
                branchId.Value,
                input.ProductId,
                input.QuantityChange,
                input.ReorderLevel,
                input.Reason,
                currentStaffId,
                CorrelationIdMiddleware.GetCorrelationId(HttpContext),
                cancellationToken);
            TempData["StatusMessage"] = "Inventory updated.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { branchId });
    }

    private bool CanManageInventory() =>
        User.IsInRole(nameof(StaffRole.Administrator)) || User.IsInRole(nameof(StaffRole.BranchManager));
}
