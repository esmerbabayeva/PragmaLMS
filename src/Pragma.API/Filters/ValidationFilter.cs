using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Pragma.API.Filters;

/// <summary>
/// Controller Action parametrlərini FluentValidation ilə yoxlayan asinxron filter.
/// Hər hansı validasiya xətası olduqda RFC 7807 ProblemDetails (HTTP 400) qaytarır.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var validationErrors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null) continue;

            var argumentType = argument.GetType();

            if (argumentType.IsPrimitive || 
                argumentType.IsEnum || 
                argumentType == typeof(string) || 
                argumentType == typeof(Guid) || 
                argumentType == typeof(DateTime) || 
                argumentType == typeof(DateTimeOffset) || 
                argumentType == typeof(TimeSpan) ||
                argumentType == typeof(decimal))
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            if (context.HttpContext.RequestServices.GetService(validatorType) is IValidator validator)
            {
                var validationContext = new ValidationContext<object>(argument);
                var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        var key = string.IsNullOrEmpty(error.PropertyName) ? "General" : error.PropertyName;
                        if (!validationErrors.TryGetValue(key, out var list))
                        {
                            list = new List<string>();
                            validationErrors[key] = list;
                        }
                        list.Add(error.ErrorMessage);
                    }
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            var problemDetails = new ValidationProblemDetails(
                validationErrors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = "One or more validation errors occurred.",
                Instance = context.HttpContext.Request.Path
            };
            problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            context.Result = new BadRequestObjectResult(problemDetails);
            return;
        }

        await next();
    }
}
