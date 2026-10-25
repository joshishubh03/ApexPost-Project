using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

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
        ("Administration", "UpdateStaff"),
        ("Administration", "SetStaffActive"),
        ("Catalog", "Create"),
        ("Catalog", "SetActive"),
        ("Inventory", "Adjust"),
        ("Shipping", "CreateZone"),
        ("Shipping", "CreateRate"),
        ("CounterTransactions", "Complete"),
        ("CounterTransactions", "Reverse"),
        ("MoneyOrders", "Issue"),
        ("MoneyOrders", "Refund")
    ];

    private static readonly HashSet<string> SafeRouteKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "branchId", "productId", "transactionId", "moneyOrderId", "terminalId", "staffId", "zoneId"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        if (!AuditedActions.Contains((controller, action)))
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        {
            await next();
            return;
        }

        var routeValues = GetSafeRouteValues(context);
        var branchId = GetBranchId(user, routeValues);
        var entityId = GetEntityId(controller, action, routeValues, context.HttpContext.TraceIdentifier);
        var role = string.Join(",", user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.Ordinal));
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
        var stopwatch = Stopwatch.StartNew();
        ActionExecutedContext? executed = null;

        try
        {
            executed = await next();
        }
        finally
        {
            stopwatch.Stop();
            var record = new AuditRecord(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                $"Mvc{controller}{action}",
                controller,
                entityId,
                actorId,
                branchId,
                correlationId,
                null,
                "MVC action request",
                string.IsNullOrEmpty(role) ? null : role,
                GetResultStatusCode(executed, context.HttpContext.Response.StatusCode),
                stopwatch.ElapsedMilliseconds,
                routeValues);
            await AppendSafelyAsync(record, controller, action, correlationId);
        }
    }

    private async Task AppendSafelyAsync(AuditRecord record, string controller, string action, string correlationId)
    {
        try
        {
            await auditLogRepository.AppendAsync(record, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Could not write MVC audit record for {Controller}/{Action} (correlation {CorrelationId}).",
                controller, action, correlationId);
        }
    }

    private static IReadOnlyDictionary<string, string> GetSafeRouteValues(ActionExecutingContext context)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in context.RouteData.Values)
        {
            AddSafeValue(values, key, value);
        }
        foreach (var (key, value) in context.ActionArguments)
        {
            AddSafeValue(values, key, value);
        }
        foreach (var argument in context.ActionArguments.Values)
        {
            switch (argument)
            {
                case CreateCounterTransactionInputModel input:
                    AddSafeValue(values, "branchId", input.BranchId);
                    AddSafeValue(values, "terminalId", input.TerminalId);
                    break;
                case ReverseCounterTransactionInputModel input:
                    AddSafeValue(values, "transactionId", input.TransactionId);
                    break;
                case RefundMoneyOrderInputModel input:
                    AddSafeValue(values, "moneyOrderId", input.MoneyOrderId);
                    break;
                case StockAdjustmentInputModel input:
                    AddSafeValue(values, "branchId", input.BranchId);
                    AddSafeValue(values, "productId", input.ProductId);
                    break;
                case CreateMoneyOrderInputModel input:
                    AddSafeValue(values, "branchId", input.BranchId);
                    break;
                case CreateTerminalInputModel input:
                    AddSafeValue(values, "branchId", input.BranchId);
                    break;
                case CreateStaffInputModel input:
                    AddSafeValue(values, "branchId", input.BranchId);
                    break;
                case UpdateStaffInputModel input:
                    AddSafeValue(values, "staffId", input.Id);
                    AddSafeValue(values, "branchId", input.BranchId);
                    break;
                case CreateShippingRateInputModel input:
                    AddSafeValue(values, "zoneId", input.ZoneId);
                    break;
            }
        }
        return values;
    }

    private static void AddSafeValue(IDictionary<string, string> destination, string key, object? value)
    {
        if (!SafeRouteKeys.Contains(key) || value is null)
        {
            return;
        }

        if (value is Guid id && id != Guid.Empty)
        {
            destination[key] = id.ToString("D");
        }
        else if (value is string text && Guid.TryParse(text, out var parsedId) && parsedId != Guid.Empty)
        {
            destination[key] = parsedId.ToString("D");
        }
    }

    private static Guid GetBranchId(ClaimsPrincipal user, IReadOnlyDictionary<string, string> routeValues)
    {
        if (Guid.TryParse(user.FindFirstValue("branch_id"), out var assignedBranchId))
        {
            return assignedBranchId;
        }
        return routeValues.TryGetValue("branchId", out var branchValue) && Guid.TryParse(branchValue, out var branchId)
            ? branchId
            : Guid.Empty;
    }

    private static string GetEntityId(
        string controller,
        string action,
        IReadOnlyDictionary<string, string> routeValues,
        string traceIdentifier)
    {
        var key = (controller, action) switch
        {
            ("CounterTransactions", "Reverse") => "transactionId",
            ("MoneyOrders", "Refund") => "moneyOrderId",
            ("Inventory", "Adjust") => "productId",
            ("Shipping", "CreateRate") => "zoneId",
            ("Administration", "SetBranchOpen") => "id",
            ("Administration", "SetTerminalActive") => "id",
            ("Administration", "SetStaffActive") => "id",
            ("Administration", "UpdateStaff") => "staffId",
            ("Catalog", "SetActive") => "id",
            _ => string.Empty
        };
        return key.Length > 0 && routeValues.TryGetValue(key, out var entityId) ? entityId : traceIdentifier;
    }

    private static int GetResultStatusCode(ActionExecutedContext? executed, int responseStatusCode)
    {
        if (executed is null || executed.Exception is not null && !executed.ExceptionHandled)
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (executed.Result is IStatusCodeActionResult { StatusCode: int statusCode })
        {
            return statusCode;
        }

        return executed.Result switch
        {
            RedirectResult or RedirectToActionResult or RedirectToRouteResult or LocalRedirectResult => StatusCodes.Status302Found,
            ForbidResult => StatusCodes.Status403Forbidden,
            UnauthorizedResult => StatusCodes.Status401Unauthorized,
            NotFoundResult => StatusCodes.Status404NotFound,
            _ => responseStatusCode
        };
    }
}
