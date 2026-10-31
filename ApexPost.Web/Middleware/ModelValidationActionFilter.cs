using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ApexPost.Application.Validation;

namespace ApexPost.Web.Middleware;

public sealed class ModelValidationActionFilter(CachedAttributeValidator attributeValidator) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            foreach (var failure in attributeValidator.Validate(argument))
            {
                context.ModelState.AddModelError(failure.MemberName, failure.ErrorMessage);
            }
        }

        if (context.ModelState.IsValid)
        {
            await next();
            return;
        }

        if (RequestsJson(context.HttpContext.Request))
        {
            context.Result = CreateValidationResponse(context);
            return;
        }

        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        if (executed.Result is not (ViewResult or PartialViewResult))
        {
            executed.Result = CreateValidationResponse(context);
        }
    }

    private static bool RequestsJson(HttpRequest request) =>
        request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true ||
        request.GetTypedHeaders().Accept?.Any(mediaType =>
            string.Equals(mediaType.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.MediaType.Value?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true) == true;

    private static BadRequestObjectResult CreateValidationResponse(ActionExecutingContext context)
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Instance = context.HttpContext.Request.Path
        };
        return new BadRequestObjectResult(problem);
    }
}
