using ApexPost.Api.Contracts;
using ApexPost.Application.Courier;
using ApexPost.Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.V1;

/// <summary>
/// Shipments API v1 - the unchanged Part B contract (flat recipient fields, numeric status). Deprecated: use v2.
/// Thin controller: bind, call <see cref="CourierShipmentService"/>, map to the v1 DTOs, return the HTTP result.
/// </summary>
[ApiController]
[ApiVersion("1.0", Deprecated = true)]
[Route("api/v{version:apiVersion}/shipments")]
[Tags("Shipments")]
[Authorize] // any authenticated user may read; writes are narrowed per action below
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class ShipmentsController(CourierShipmentService shipments) : ControllerBase
{
    /// <summary>Lists shipments (v1), newest first, with paging and optional status/city filters.</summary>
    /// <param name="query">page (default 1), pageSize (default 20, max 50), status, city.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">One page of shipments.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet(Name = "ListShipmentsV1")]
    [EndpointSummary("List shipments (v1, deprecated)")]
    [ProducesResponseType<ShipmentPageResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ShipmentPageResponse>> List([FromQuery] ShipmentListQuery query, CancellationToken cancellationToken) =>
        Ok((await shipments.ListAsync(query.ToQuery(), cancellationToken)).ToResponse());

    /// <summary>Gets one shipment by tracking number (v1).</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">The shipment.</response>
    /// <response code="404">Unknown tracking number.</response>
    [HttpGet("{trackingNumber:trackingNumber}", Name = "GetShipmentV1")]
    [EndpointSummary("Get a shipment (v1, deprecated)")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponse>> Get(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await shipments.GetAsync(trackingNumber, cancellationToken)).ToResponse());

    /// <summary>Creates a shipment in status Created (v1).</summary>
    /// <param name="request">The new shipment.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="401">Missing, invalid or expired token.</response>
    /// <response code="403">Admin role required.</response>
    /// <response code="201">Created; the Location header points to the new shipment.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="409">The tracking number already exists.</response>
    /// <response code="415">The body is not application/json.</response>
    [Authorize(Roles = ApexPostClaims.AdminRole)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [HttpPost(Name = "CreateShipmentV1")]
    [Consumes("application/json")]
    [EndpointSummary("Create a shipment (v1, deprecated)")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponse>> Create(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var created = (await shipments.CreateAsync(request.ToCommand(), cancellationToken)).ToResponse();
        return CreatedAtAction(nameof(Get), new { version = "1", trackingNumber = created.TrackingNumber }, created);
    }

    /// <summary>Replaces the editable details (weight and recipient) (v1).</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="request">The new details.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="401">Missing, invalid or expired token.</response>
    /// <response code="403">Admin role required.</response>
    /// <response code="200">The updated shipment.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="404">Unknown tracking number.</response>
    /// <response code="409">Delivered/Failed shipments cannot be edited, or a concurrent change won.</response>
    /// <response code="415">The body is not application/json.</response>
    [Authorize(Roles = ApexPostClaims.AdminRole)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [HttpPut("{trackingNumber:trackingNumber}", Name = "ReplaceShipmentV1")]
    [Consumes("application/json")]
    [EndpointSummary("Replace shipment details (v1, deprecated)")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponse>> Replace(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.ReplaceAsync(trackingNumber, request.ToCommand(), cancellationToken)).ToResponse());

    /// <summary>Changes the status only; must follow Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered/Failed (v1).</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="request">The new status.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="401">Missing, invalid or expired token.</response>
    /// <response code="403">Courier role required.</response>
    /// <response code="200">The updated shipment.</response>
    /// <response code="400">Missing or unknown status.</response>
    /// <response code="404">Unknown tracking number.</response>
    /// <response code="409">The transition is not allowed.</response>
    /// <response code="415">The body is not application/json.</response>
    [Authorize(Roles = ApexPostClaims.CourierRole)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [HttpPatch("{trackingNumber:trackingNumber}/status", Name = "UpdateShipmentStatusV1")]
    [Consumes("application/json")]
    [EndpointSummary("Change shipment status (v1, deprecated)")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponse>> UpdateStatus(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.ChangeStatusAsync(trackingNumber, request.Status!.Value, cancellationToken)).ToResponse());

    /// <summary>Deletes a shipment; only allowed while its status is Created (v1).</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="401">Missing, invalid or expired token.</response>
    /// <response code="403">Admin role required.</response>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Unknown tracking number.</response>
    /// <response code="409">The shipment is no longer in status Created.</response>
    [Authorize(Roles = ApexPostClaims.AdminRole)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [HttpDelete("{trackingNumber:trackingNumber}", Name = "DeleteShipmentV1")]
    [EndpointSummary("Delete a shipment (v1, deprecated)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string trackingNumber, CancellationToken cancellationToken)
    {
        await shipments.DeleteAsync(trackingNumber, cancellationToken);
        return NoContent();
    }
}
