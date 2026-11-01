using Microsoft.AspNetCore.Mvc;
using Pragma.Application.Common;

namespace Pragma.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }

        return HandleError(result.Error);
    }

    protected ActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return HandleError(result.Error);
    }

    private ActionResult HandleError(Error error)
    {
        var problemDetails = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Instance = HttpContext.Request.Path
        };

        return error.Type switch
        {
            ErrorType.NotFound => NotFound(problemDetails),
            ErrorType.Validation => BadRequest(problemDetails),
            ErrorType.Conflict => Conflict(problemDetails),
            ErrorType.Unauthorized => Unauthorized(problemDetails),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, problemDetails),
            _ => BadRequest(problemDetails)
        };
    }
}
