using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using VehicleGo.Domain.Common;

namespace VehicleGo.Web.Errors;

/// <summary>
/// Error codes (design §1.5) → HTTP status and ProblemDetails (<c>type</c>, <c>title</c>, <c>status</c>,
/// <c>traceId</c>, extension <c>code</c>, plus the error's extensions). <c>VALIDATION_FAILED</c> adds
/// <c>errors: { field: [message] }</c> with the camelCase JSON field names of the request.
/// </summary>
public static class ErrorCodeMapping
{
    public const string ProblemJson = "application/problem+json";
    public const string ValidationTitle = "One or more validation errors occurred.";

    public static int StatusOf(ErrorCode code) => code switch
    {
        ErrorCode.ValidationFailed
            or ErrorCode.IssueBeforeManufacture
            or ErrorCode.DateInFuture
            or ErrorCode.BracketTypeMismatch
            or ErrorCode.LicenceDateNotAllowed
            or ErrorCode.LicenceDateBeforeIssue
            or ErrorCode.LicenceDateTooFar
            or ErrorCode.CsrfInvalid => StatusCodes.Status400BadRequest,
        ErrorCode.VehicleNotFound => StatusCodes.Status404NotFound,
        ErrorCode.PlateExistsActive
            or ErrorCode.PlateExistsInTrash
            or ErrorCode.VehicleLimitReached
            or ErrorCode.VehicleModified
            or ErrorCode.VehicleInTrash
            or ErrorCode.VehicleNotInTrash => StatusCodes.Status409Conflict,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code."),
    };

    /// <summary>The fixed code string of an error code, e.g. <c>PLATE_EXISTS_IN_TRASH</c>.</summary>
    public static string CodeOf(ErrorCode code)
    {
        var name = code.ToString();
        return typeof(ErrorCode).GetField(name)?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? name;
    }

    public static IActionResult ToActionResult(HttpContext httpContext, Error error)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(error);

        var status = StatusOf(error.Code);
        var problem = Create(httpContext, status, error.Code == ErrorCode.ValidationFailed ? ValidationTitle : null);
        problem.Extensions["code"] = CodeOf(error.Code);
        foreach (var (key, value) in error.Extensions)
        {
            problem.Extensions[key] = value;
        }

        if (error.Code == ErrorCode.ValidationFailed)
        {
            var field = error.Field ?? string.Empty;
            problem.Extensions["errors"] = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [field] = [$"The {field} field is invalid."],
            };
        }

        return Result(problem, status);
    }

    /// <summary>
    /// <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c>: every framework 400 (missing required
    /// value, JSON parse error, unknown code) becomes <c>VALIDATION_FAILED</c>. JSON paths lose their
    /// <c>$.</c> prefix and the entry named after the action parameter (e.g. "The input field is required.") is dropped.
    /// </summary>
    public static IActionResult InvalidModelState(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var parameterNames = context.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0 || parameterNames.Contains(key))
            {
                continue;
            }

            var field = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
            var messages = entry.Errors
                .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? $"The {field} field is invalid." : e.ErrorMessage);
            errors[field] = errors.TryGetValue(field, out var existing) ? [.. existing, .. messages] : [.. messages];
        }

        var status = StatusCodes.Status400BadRequest;
        var problem = Create(context.HttpContext, status, ValidationTitle);
        problem.Extensions["code"] = CodeOf(ErrorCode.ValidationFailed);
        problem.Extensions["errors"] = errors;
        return Result(problem, status);
    }

    private static ProblemDetails Create(HttpContext httpContext, int status, string? title)
    {
        var factory = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        return factory.CreateProblemDetails(httpContext, status, title);
    }

    private static ObjectResult Result(ProblemDetails problem, int status) =>
        new(problem) { StatusCode = status, ContentTypes = { ProblemJson } };
}
