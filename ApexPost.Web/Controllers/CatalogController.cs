using System.Security.Claims;
using ApexPost.Application.Queries;
using ApexPost.Application.Services;
using ApexPost.Web.Middleware;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class CatalogController(
    ProductCatalogueService catalogueService,
    IPriceRevisionService? priceRevisionService = null) : Controller
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

    /// <summary>Set-based price revision for one category (ExecuteUpdateAsync, SQL Server only).</summary>
    [HttpPost]
    [Authorize(Roles = "Administrator,BranchManager")]
    public async Task<IActionResult> RevisePrices(
        [Bind(nameof(RevisePricesInputModel.Category), nameof(RevisePricesInputModel.PercentChange))] RevisePricesInputModel input,
        CancellationToken cancellationToken)
    {
        if (priceRevisionService is null)
        {
            TempData["ErrorMessage"] = "Price revision requires the SQL Server persistence provider.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid || input.PercentChange == 0)
        {
            TempData["ErrorMessage"] = "Enter a category and a non-zero change between -50% and +100%.";
            return RedirectToAction(nameof(Index));
        }

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        {
            return Forbid();
        }

        try
        {
            var updated = await priceRevisionService.RevisePricesAsync(
                input.Category, input.PercentChange, actorId,
                CorrelationIdMiddleware.GetCorrelationId(HttpContext), cancellationToken);
            catalogueService.InvalidateCache();
            TempData["StatusMessage"] = $"{updated} product price(s) in '{input.Category.Trim()}' changed by {input.PercentChange:0.##}%.";
        }
        catch (KeyNotFoundException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private bool CanManageProducts() =>
        User.IsInRole(nameof(StaffRole.Administrator)) || User.IsInRole(nameof(StaffRole.BranchManager));
}
