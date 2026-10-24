using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Enums;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ShippingController(ShippingRateService shippingRateService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildPageModelAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = nameof(StaffRole.Administrator))]
    public async Task<IActionResult> CreateZone(
        [Bind(Prefix = "NewZone")] CreateShippingZoneInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Enter a valid zone code, name, and destination.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await shippingRateService.CreateZoneAsync(input.Code, input.Name, input.Destination, cancellationToken);
            TempData["StatusMessage"] = $"Shipping zone {input.Code.Trim().ToUpperInvariant()} was created.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = nameof(StaffRole.Administrator))]
    public async Task<IActionResult> CreateRate(
        [Bind(Prefix = "NewRate")] CreateShippingRateInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || input.ZoneId is null)
        {
            TempData["ErrorMessage"] = "Enter a valid zone and rate configuration.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await shippingRateService.CreateRateAsync(
                input.ZoneId.Value,
                input.ServiceLevel,
                input.BasePrice,
                input.PricePerKilogram,
                input.MaximumWeightKilograms,
                cancellationToken);
            TempData["StatusMessage"] = "Shipping rate was created.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Quote(
        [Bind(Prefix = "QuoteInput")] ShippingQuoteInputModel input,
        CancellationToken cancellationToken)
    {
        var page = await BuildPageModelAsync(cancellationToken);
        if (!ModelState.IsValid || input.RateId is null)
        {
            ModelState.AddModelError(string.Empty, "Select a rate and enter a valid parcel weight.");
            page = page with { QuoteInput = input };
            return View("Index", page);
        }

        try
        {
            var quote = await shippingRateService.QuoteAsync(input.RateId.Value, input.WeightKilograms, cancellationToken);
            return View("Index", page with { QuoteInput = input, Quote = quote });
        }
        catch (KeyNotFoundException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return View("Index", page with { QuoteInput = input });
    }

    private async Task<ShippingPageModel> BuildPageModelAsync(CancellationToken cancellationToken)
    {
        var isAdministrator = User.IsInRole(nameof(StaffRole.Administrator));
        var schedules = await shippingRateService.GetScheduleAsync(isAdministrator, cancellationToken);
        var activeSchedules = schedules.Where(schedule => schedule.Zone.IsActive).ToArray();

        return new ShippingPageModel
        {
            Schedules = schedules,
            ActiveZones = activeSchedules.Select(schedule => schedule.Zone).ToArray(),
            ActiveRates = activeSchedules.SelectMany(schedule => schedule.Rates).Where(rate => rate.IsActive).ToArray()
        };
    }
}
