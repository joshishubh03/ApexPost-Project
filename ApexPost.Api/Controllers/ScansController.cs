using ApexPost.Api.Contracts;
using ApexPost.Application.Courier;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers;

/// <summary>
/// Scan history and idempotent bulk upload of offline scans. The scan contract did not change between versions,
/// so one controller serves v1 (deprecated) and v2.
/// </summary>
[ApiController]
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/shipments/{trackingNumber:trackingNumber}/scans")]
[Tags("Scans")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class ScansController(CourierShipmentService shipments) : ControllerBase
{
    /// <summary>
    /// Uploads offline scans from the courier device. Idempotent: a scan ID that is already stored (or repeated in
    /// the batch) is counted in duplicatesSkipped and never stored twice, so the app can safely retry.
    /// </summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="request">1 to 100 scans, each with its scan::{Guid} ID.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">Counts of accepted, duplicate and rejected scans (with reasons).</response>
    /// <response code="400">Validation failure (for example a scan in the future, or Delivered without a signature).</response>
    /// <response code="404">Unknown tracking number.</response>
    /// <response code="415">The body is not application/json.</response>
    [HttpPost(Name = "UploadScans")]
    [Consumes("application/json")]
    [EndpointSummary("Upload offline scans (idempotent)")]
    [ProducesResponseType<ScanUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ScanUploadResponse>> Upload(string trackingNumber, ScanUploadRequest request, CancellationToken cancellationToken) =>
        Ok((await shipments.UploadScansAsync(trackingNumber, request.ToItems(), cancellationToken: cancellationToken)).ToResponse());

    /// <summary>Scan history for a shipment, oldest first.</summary>
    /// <param name="trackingNumber">APX-######## tracking number.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <response code="200">The scans.</response>
    /// <response code="404">Unknown tracking number.</response>
    [HttpGet(Name = "GetScanHistory")]
    [EndpointSummary("Get scan history")]
    [ProducesResponseType<IReadOnlyList<ScanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ScanResponse>>> History(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await shipments.GetScanHistoryAsync(trackingNumber, cancellationToken)).Select(scan => scan.ToResponse()).ToArray());
}
