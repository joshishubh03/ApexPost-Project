using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Roles = nameof(StaffRole.Administrator))]
public sealed class AdministrationController(StaffManagementService staffManagementService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Branches(CancellationToken cancellationToken)
    {
        await PopulateBranchesPageAsync(cancellationToken);
        return View(new CreateBranchInputModel());
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranch(CreateBranchInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBranchesPageAsync(cancellationToken);
            return View("Branches", input);
        }

        try
        {
            if (input.OpeningTime >= input.ClosingTime)
                throw new ArgumentException("Closing time must be later than opening time.");
            await staffManagementService.CreateBranchAsync(input.Code, input.Name, input.Region,
                input.OpeningTime, input.ClosingTime, cancellationToken);
            TempData["StatusMessage"] = $"Branch {input.Code.Trim().ToUpperInvariant()} was created.";
            return RedirectToAction(nameof(Branches));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(input.Code), exception.Message);
            await PopulateBranchesPageAsync(cancellationToken);
            return View("Branches", input);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateBranchesPageAsync(cancellationToken);
            return View("Branches", input);
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetBranchOpen(Guid id, bool isOpen, CancellationToken cancellationToken)
    {
        try
        {
            await staffManagementService.SetBranchOpenStatusAsync(id, isOpen, cancellationToken);
            TempData["StatusMessage"] = isOpen ? "Branch opened for operations." : "Branch closed for operations.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Branches));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTerminal(CreateTerminalInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Enter a valid branch, terminal code, and name.";
            return RedirectToAction(nameof(Branches));
        }
        try
        {
            await staffManagementService.CreateTerminalAsync(input.BranchId, input.Code, input.Name, cancellationToken);
            TempData["StatusMessage"] = $"Terminal {input.Code.Trim().ToUpperInvariant()} was created.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        return RedirectToAction(nameof(Branches));
    }

    [HttpPost]
    public async Task<IActionResult> SetTerminalActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        try
        {
            await staffManagementService.SetTerminalActiveAsync(id, isActive, cancellationToken);
            TempData["StatusMessage"] = isActive ? "Terminal activated." : "Terminal deactivated.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Branches));
    }

    [HttpGet]
    public async Task<IActionResult> Staff(CancellationToken cancellationToken)
    {
        await PopulateStaffPageAsync(cancellationToken);
        return View(new CreateStaffInputModel());
    }

    [HttpGet]
    public async Task<IActionResult> EditStaff(Guid id, CancellationToken cancellationToken)
    {
        var member = (await staffManagementService.GetStaffAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == id);
        if (member is null)
        {
            return NotFound();
        }

        ViewData["Branches"] = await staffManagementService.GetBranchesAsync(cancellationToken);
        return View(new UpdateStaffInputModel
        {
            Id = member.Id,
            Email = member.Email,
            FullName = member.FullName,
            Role = member.Role,
            BranchId = member.BranchId
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStaff(UpdateStaffInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (!await PopulateStaffEditBranchesAsync(input.Id, cancellationToken))
            {
                return NotFound();
            }
            return View("EditStaff", input);
        }

        try
        {
            await staffManagementService.UpdateStaffAsync(
                input.Id, input.Email, input.FullName, input.Role, input.BranchId, cancellationToken);
            TempData["StatusMessage"] = $"Staff account for {input.Email.Trim()} was updated.";
            return RedirectToAction(nameof(Staff));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (!await PopulateStaffEditBranchesAsync(input.Id, cancellationToken))
            {
                return NotFound();
            }
            return View("EditStaff", input);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateStaff(CreateStaffInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateStaffPageAsync(cancellationToken);
            return View("Staff", input);
        }

        try
        {
            await staffManagementService.CreateStaffAsync(
                input.Email, input.FullName, input.Role, input.BranchId, input.InitialPassword, cancellationToken);
            TempData["StatusMessage"] = $"Staff account for {input.Email.Trim()} was created.";
            return RedirectToAction(nameof(Staff));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateStaffPageAsync(cancellationToken);
            return View("Staff", input);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateStaffPageAsync(cancellationToken);
            return View("Staff", input);
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetStaffActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        try
        {
            await staffManagementService.SetStaffActiveAsync(id, isActive, cancellationToken);
            TempData["StatusMessage"] = isActive ? "Staff account activated." : "Staff account deactivated.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Staff));
    }

    private async Task PopulateStaffPageAsync(CancellationToken cancellationToken)
    {
        ViewData["Staff"] = await staffManagementService.GetStaffAsync(cancellationToken);
        ViewData["Branches"] = await staffManagementService.GetBranchesAsync(cancellationToken);
    }

    private async Task<bool> PopulateStaffEditBranchesAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var staff = await staffManagementService.GetStaffAsync(cancellationToken);
        if (staff.All(member => member.Id != staffId))
        {
            return false;
        }
        ViewData["Branches"] = await staffManagementService.GetBranchesAsync(cancellationToken);
        return true;
    }

    private async Task PopulateBranchesPageAsync(CancellationToken cancellationToken)
    {
        ViewData["Branches"] = await staffManagementService.GetBranchesAsync(cancellationToken);
        ViewData["Terminals"] = await staffManagementService.GetTerminalsAsync(cancellationToken);
    }
}
