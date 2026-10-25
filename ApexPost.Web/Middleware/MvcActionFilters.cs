using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Middleware;

public sealed class RequestTimingActionFilter(ILogger<RequestTimingActionFilter> logger, IConfiguration configuration)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next();
        }
        finally
        {
            stopwatch.Stop();
            var threshold = configuration.GetValue<int>("Diagnostics:SlowActionThresholdMilliseconds", 1000);
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();
            if (stopwatch.ElapsedMilliseconds >= threshold)
            {
                logger.LogWarning("MVC action {Controller}/{Action} took {ElapsedMilliseconds} ms.",
                    controller, action, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

public sealed class MvcAuditActionFilter(
    IAuditLogRepository auditLogRepository,
    ILogger<MvcAuditActionFilter> logger) : IAsyncActionFilter
{
    private static readonly HashSet<(string Controller, string Action)> AuditedActions =
    [
        ("Administration", "CreateBranch"),
        ("Administration", "SetBranchOpen"),
        ("Administration", "CreateTerminal"),
        ("Administration", "SetTerminalActive"),
        ("Administration", "CreateStaff"),
        ("Administration", "SetStaffActive"),
        ("Catalog", "Create"),
        ("Catalog", "SetActive"),
        ("Inventory", "Adjust"),
        ("Shipping", "CreateZone"),
        ("Shipping", "CreateRate")
    ];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        if (!AuditedActions.Contains((controller, action)) ||
            context.Controller is not Controller mvcController ||
            mvcController.TempData["StatusMessage"] is not string)
        {
            return;
        }

        var user = context.HttpContext.User;
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        {
            return;
        }

        var branchId = Guid.TryParse(user.FindFirstValue("branch_id"), out var assignedBranchId)
            ? assignedBranchId
            : context.ActionArguments.Values.OfType<Guid>().FirstOrDefault();
        var entityId = context.RouteData.Values["id"]?.ToString() ?? context.HttpContext.TraceIdentifier;
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);

        try
        {
            await auditLogRepository.AppendAsync(new AuditRecord(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                $"Mvc{controller}{action}",
                controller,
                entityId,
                actorId,
                branchId,
                correlationId,
                null), CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Could not write MVC audit record for {Controller}/{Action} (correlation {CorrelationId}).",
                controller, action, correlationId);
        }
    }
}
