using ApexPost.Application.Services;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net.Http;
using System.Text.Json;

namespace ApexPost.Web.Controllers;

[ApiController]
[Route("api/partner/tracking")]
[EnableCors("TrackingWidget")]
[EnableRateLimiting("partner")]
[AllowAnonymous]
[ProtectedIntegrationEndpoint]
public sealed class PartnerTrackingController(PartnerTrackingService trackingService) : ControllerBase
{
    [HttpGet("{trackingNumber}")]
    public async Task<IActionResult> Get(string trackingNumber, CancellationToken cancellationToken)
    {
        try
        {
            var shipment = await trackingService.FindAsync(trackingNumber, cancellationToken);
            return shipment is null ? NotFound() : Ok(shipment);
        }
        catch (TimeoutException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Courier tracking is temporarily unavailable." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Courier tracking is temporarily unavailable." });
        }
        catch (InvalidDataException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The courier returned an invalid tracking response." });
        }
        catch (JsonException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The courier returned an invalid tracking response." });
        }
    }
}
