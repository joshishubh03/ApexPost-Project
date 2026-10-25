using ApexPost.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

[ApiController]
[Route("api/partner/tracking")]
[EnableCors("TrackingWidget")]
[EnableRateLimiting("partner")]
[AllowAnonymous]
public sealed class PartnerTrackingController(PartnerTrackingService trackingService) : ControllerBase
{
    [HttpGet("{trackingNumber}")]
    public async Task<IActionResult> Get(string trackingNumber, CancellationToken cancellationToken)
    {
        var shipment = await trackingService.FindAsync(trackingNumber, cancellationToken);
        return shipment is null ? NotFound() : Ok(shipment);
    }
}
