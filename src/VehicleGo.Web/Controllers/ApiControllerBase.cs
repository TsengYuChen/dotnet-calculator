using Microsoft.AspNetCore.Mvc;
using VehicleGo.Domain.Common;
using VehicleGo.Web.Errors;

namespace VehicleGo.Web.Controllers;

/// <summary>Shared mapping of handler results: success → the given result, failure → ProblemDetails.</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Respond<T>(Result<T> result, Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : ErrorCodeMapping.ToActionResult(HttpContext, result.Error);
    }

    protected IActionResult Respond(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? NoContent() : ErrorCodeMapping.ToActionResult(HttpContext, result.Error);
    }
}
