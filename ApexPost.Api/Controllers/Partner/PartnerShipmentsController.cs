using ApexPost.Api.Contracts;
using ApexPost.Api.Security;
using ApexPost.Application.Courier;
using ApexPost.Domain.Courier;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.Partner;

/// <summary>
/// Read-only tracking for approved partner systems. Authenticated by the X-Api-Key header
/// (<see cref="PartnerApiKeyMiddleware"/>), not by JWT. Returns no recipient personal data.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/partner/shipments")]
[AllowAnonymous]
[Tags("Partner")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class PartnerShipmentsController(CourierShipmentService shipments) : ControllerBase
{
    /// <summary>Tracking status of one shipment for a partner (requires the X-Api-Key header).</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">Status, destination city and latest scan.</response>
    /// <response code="401">Missing or invalid X-Api-Key.</response>
    /// <response code="404">Unknown tracking number.</response>
    [HttpGet("{trackingNumber:trackingNumber}", Name = "GetPartnerShipment")]
    [EndpointSummary("Track a shipment (partner, API key)")]
    [ProducesResponseType<PartnerShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PartnerShipmentResponse>> Get(string trackingNumber, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetAsync(trackingNumber, cancellationToken);
        return Ok(new PartnerShipmentResponse
        {
            TrackingNumber = shipment.TrackingNumber,
            Status = shipment.Status,
            DestinationCity = shipment.Recipient.City,
            LatestScan = shipment.LatestScan is { } latest
                ? new LatestScanResponse { ScanType = latest.ScanType, Location = latest.Location, ScannedAtUtc = latest.ScannedAtUtc }
                : null,
            UpdatedAtUtc = shipment.UpdatedAtUtc
        });
    }
}

/// <summary>Partner view of a shipment: no recipient name, phone or address.</summary>
public sealed class PartnerShipmentResponse
{
    /// <summary>Tracking number.</summary>
    public required string TrackingNumber { get; init; }

    /// <summary>Status name.</summary>
    public required CourierShipmentStatus Status { get; init; }

    /// <summary>Destination city only.</summary>
    public required string DestinationCity { get; init; }

    /// <summary>The most recent scan, or null.</summary>
    public LatestScanResponse? LatestScan { get; init; }

    /// <summary>When the shipment last changed (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
