using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Authorization;

public sealed class SameBranchRequirement : IAuthorizationRequirement
{
}

public sealed class SameBranchAuthorizationHandler : AuthorizationHandler<SameBranchRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameBranchRequirement requirement)
    {
        if (context.User.IsInRole("Administrator"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var assignedBranch = context.User.FindFirstValue("branch_id");
        if (!Guid.TryParse(assignedBranch, out var assignedBranchId))
            return Task.CompletedTask;

        var requestedBranch = GetRequestedBranch(context.Resource);
        if (requestedBranch is null ||
            (Guid.TryParse(requestedBranch, out var requestedBranchId) && requestedBranchId == assignedBranchId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static string? GetRequestedBranch(object? resource)
    {
        if (resource is not HttpContext httpContext)
            return null;

        var routeBranch = httpContext.Request.RouteValues.TryGetValue("branchId", out var routeValue)
            ? routeValue?.ToString()
            : null;
        return routeBranch ?? httpContext.Request.Query["branchId"].FirstOrDefault();
    }
}
