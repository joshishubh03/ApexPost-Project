using ApexPost.Api.Contracts;
using ApexPost.Application.Courier;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers;

/// <summary>
/// Shipments resource. Thin controller: bind the input, call <see cref="CourierShipmentService"/>, return the HTTP
/// result. Business rules (status machine, duplicates, delete-only-while-Created) live in the service and domain;
/// errors become Problem Details in ApiExceptionHandler.
/// </summary>
[ApiController]
[Route("api/shipments")]
[Produces("application/json")]
public sealed class ShipmentsController(CourierShipmentService shipments) : ControllerBase
{
    /// <summary>Lists shipments, newest first, with paging and optional status/city filters.</summary>
    [HttpGet(Name = "ListShipments")]
    public async Task<ActionResult<ShipmentPageResponse>> List([FromQuery] ShipmentListQuery query, CancellationToken cancellationToken) =>
        Ok((await shipments.ListAsync(query.ToQuery(), cancellationToken)).ToResponse());

    /// <summary>Gets one shipment by tracking number.</summary>
    [HttpGet("{trackingNumber:trackingNumber}", Name = "GetShipment")]
    public async Task<ActionResult<ShipmentResponse>> Get(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await shipments.GetAsync(trackingNumber, cancellationToken)).ToResponse());

    /// <summary>Creates a shipment (status Created). Returns 201 with a Location header.</summary>
    [HttpPost(Name = "CreateShipment")]
    [Consumes("application/json")]
    public async Task<ActionResult<ShipmentResponse>> Create(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var created = (await shipments.CreateAsync(request.ToCommand(), cancellationToken)).ToResponse();
        return CreatedAtAction(nameof(Get), new { trackingNumber = created.TrackingNumber }, created);
    }

    /// <summary>Replaces the editable details (weight and recipient).</summary>
    [HttpPut("{trackingNumber:trackingNumber}", Name = "ReplaceShipment")]
    [Consumes("application/json")]
    public async Task<ActionResult<ShipmentResponse>> Replace(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.ReplaceAsync(trackingNumber, request.ToCommand(), cancellationToken)).ToResponse());

    /// <summary>Changes the status only (must follow the allowed transitions).</summary>
    [HttpPatch("{trackingNumber:trackingNumber}/status", Name = "UpdateShipmentStatus")]
    [Consumes("application/json")]
    public async Task<ActionResult<ShipmentResponse>> UpdateStatus(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.ChangeStatusAsync(trackingNumber, request.Status!.Value, cancellationToken)).ToResponse());

    /// <summary>Deletes a shipment; only allowed while its status is Created.</summary>
    [HttpDelete("{trackingNumber:trackingNumber}", Name = "DeleteShipment")]
    public async Task<IActionResult> Delete(string trackingNumber, CancellationToken cancellationToken)
    {
        await shipments.DeleteAsync(trackingNumber, cancellationToken);
        return NoContent();
    }
}
