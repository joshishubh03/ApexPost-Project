using ApexPost.Api.Contracts;
using ApexPost.Application.Courier;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.V2;

/// <summary>
/// Shipments API v2 - nested recipient, latestScan summary, status as a string enum. Same service as v1; only the
/// DTOs and mapping differ. Thin controller: bind, call <see cref="CourierShipmentService"/>, map, return.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/shipments")]
[Tags("Shipments")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class ShipmentsController(CourierShipmentService shipments) : ControllerBase
{
    /// <summary>Lists shipments (v2), newest first, with paging and optional status/city filters.</summary>
    /// <param name="query">page (default 1), pageSize (default 20, max 50), status, city.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">One page of shipments.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet(Name = "ListShipments")]
    [EndpointSummary("List shipments")]
    [ProducesResponseType<ShipmentPageResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ShipmentPageResponseV2>> List([FromQuery] ShipmentListQuery query, CancellationToken cancellationToken) =>
        Ok((await shipments.ListAsync(query.ToQuery(), cancellationToken)).ToResponseV2());

    /// <summary>Gets one shipment by tracking number.</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">The shipment.</response>
    /// <response code="404">Unknown tracking number.</response>
    [HttpGet("{trackingNumber:trackingNumber}", Name = "GetShipment")]
    [EndpointSummary("Get a shipment")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponseV2>> Get(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await shipments.GetAsync(trackingNumber, cancellationToken)).ToResponseV2());

    /// <summary>Creates a shipment in status Created.</summary>
    /// <param name="request">The new shipment.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="201">Created; the Location header points to the new shipment.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="409">The tracking number already exists.</response>
    /// <response code="415">The body is not application/json.</response>
    [HttpPost(Name = "CreateShipment")]
    [Consumes("application/json")]
    [EndpointSummary("Create a shipment")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponseV2>> Create(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var created = (await shipments.CreateAsync(request.ToCommand(), cancellationToken)).ToResponseV2();
        return CreatedAtAction(nameof(Get), new { version = "2", trackingNumber = created.TrackingNumber }, created);
    }

    /// <summary>Replaces the editable details (weight and recipient).</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="request">The new details.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">The updated shipment.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="404">Unknown tracking number.</response>
    /// <response code="409">Delivered/Failed shipments cannot be edited, or a concurrent change won.</response>
    /// <response code="415">The body is not application/json.</response>
    [HttpPut("{trackingNumber:trackingNumber}", Name = "ReplaceShipment")]
    [Consumes("application/json")]
    [EndpointSummary("Replace shipment details")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponseV2>> Replace(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.ReplaceAsync(trackingNumber, request.ToCommand(), cancellationToken)).ToResponseV2());

    /// <summary>Changes the status only; must follow Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered/Failed.</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="request">The new status.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">The updated shipment.</response>
    /// <response code="400">Missing or unknown status.</response>
    /// <response code="404">Unknown tracking number.</response>
    /// <response code="409">The transition is not allowed.</response>
    /// <response code="415">The body is not application/json.</response>
    [HttpPatch("{trackingNumber:trackingNumber}/status", Name = "UpdateShipmentStatus")]
    [Consumes("application/json")]
    [EndpointSummary("Change shipment status")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ShipmentResponseV2>> UpdateStatus(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.ChangeStatusAsync(trackingNumber, request.Status!.Value, cancellationToken)).ToResponseV2());

    /// <summary>Deletes a shipment; only allowed while its status is Created.</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Unknown tracking number.</response>
    /// <response code="409">The shipment is no longer in status Created.</response>
    [HttpDelete("{trackingNumber:trackingNumber}", Name = "DeleteShipment")]
    [EndpointSummary("Delete a shipment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(string trackingNumber, CancellationToken cancellationToken)
    {
        await shipments.DeleteAsync(trackingNumber, cancellationToken);
        return NoContent();
    }
}
