using ApexPost.Api.Contracts;
using ApexPost.Application.Courier;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers;

/// <summary>Scan history and idempotent bulk upload of offline scans for one shipment.</summary>
[ApiController]
[Route("api/shipments/{trackingNumber:trackingNumber}/scans")]
[Produces("application/json")]
public sealed class ScansController(CourierShipmentService shipments) : ControllerBase
{
    /// <summary>
    /// Uploads offline scans. Idempotent: a scan ID that is already stored is skipped (duplicatesSkipped), never
    /// stored twice, so the courier app can safely retry.
    /// </summary>
    [HttpPost(Name = "UploadScans")]
    [Consumes("application/json")]
    public async Task<ActionResult<ScanUploadResponse>> Upload(string trackingNumber, ScanUploadRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.UploadScansAsync(trackingNumber, request.ToItems(), cancellationToken: cancellationToken)).ToResponse());

    /// <summary>Scan history, oldest first.</summary>
    [HttpGet(Name = "GetScanHistory")]
    public async Task<ActionResult<IReadOnlyList<ScanResponse>>> History(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await shipments.GetScanHistoryAsync(trackingNumber, cancellationToken)).Select(scan => scan.ToResponse()).ToArray());
}
