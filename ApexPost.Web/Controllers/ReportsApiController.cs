using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>
/// JSON read endpoints for head office tools (query side). Invalid input returns ValidationProblemDetails (400);
/// an unknown branch surfaces as KeyNotFoundException and is mapped to a 404 Problem Details response.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Administrator,BranchManager,Auditor")]
public sealed class ReportsApiController(IReportingQueries? reportingQueries = null) : ControllerBase
{
    [HttpGet("daily-sales")]
    public async Task<ActionResult<IReadOnlyList<DailyBranchSalesDto>>> DailySales(
        [FromQuery, Required, StringLength(12, MinimumLength = 2)] string branchCode,
        [FromQuery, Required] DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (reportingQueries is null)
            return Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Reports need the SQL Server provider.");
        if (date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(date), "The sales date cannot be in the future.");
            return ValidationProblem(ModelState);
        }

        return Ok(await reportingQueries.GetDailyBranchSalesAsync(branchCode, date!.Value, cancellationToken));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<LowStockDto>>> LowStock(
        [FromQuery, StringLength(12, MinimumLength = 2)] string? branchCode,
        [FromQuery, Range(0, 1000)] int threshold = 10,
        CancellationToken cancellationToken = default)
    {
        if (reportingQueries is null)
            return Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Reports need the SQL Server provider.");
        return Ok(await reportingQueries.GetLowStockAsync(branchCode, threshold, cancellationToken));
    }
}
