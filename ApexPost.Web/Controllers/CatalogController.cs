using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class CatalogController(ProductCatalogueService catalogueService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? query, CancellationToken cancellationToken)
    {
        var canManage = CanManageProducts();
        var model = new ProductCataloguePageModel
        {
            Query = query,
            Products = await catalogueService.SearchAsync(query, canManage, cancellationToken)
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,BranchManager")]
    public async Task<IActionResult> Create(ProductCataloguePageModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await Index(input.Query, cancellationToken);
        }

        try
        {
            await catalogueService.CreateAsync(
                input.NewProduct.Sku,
                input.NewProduct.Name,
                input.NewProduct.Description,
                input.NewProduct.Category,
                input.NewProduct.Price,
                input.NewProduct.TaxCategory,
                cancellationToken);
            TempData["StatusMessage"] = $"Product {input.NewProduct.Sku.Trim().ToUpperInvariant()} was created.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("NewProduct.Sku", exception.Message);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        var model = new ProductCataloguePageModel
        {
            Query = input.Query,
            NewProduct = input.NewProduct,
            Products = await catalogueService.SearchAsync(input.Query, includeInactive: true, cancellationToken)
        };
        return View("Index", model);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,BranchManager")]
    public async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        try
        {
            await catalogueService.SetActiveAsync(id, isActive, cancellationToken);
            TempData["StatusMessage"] = isActive ? "Product activated." : "Product deactivated.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool CanManageProducts() =>
        User.IsInRole(nameof(StaffRole.Administrator)) || User.IsInRole(nameof(StaffRole.BranchManager));
}
