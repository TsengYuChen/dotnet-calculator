using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;
using VehicleGo.Domain.Common;
using VehicleGo.Web.Errors;

namespace VehicleGo.Web.Security;

/// <summary>
/// Global CSRF check with the framework's <see cref="IAntiforgery"/> (header <c>X-XSRF-TOKEN</c>). The login
/// check runs first (authorization middleware, then this filter's <see cref="Order"/> after other
/// authorization filters); safe methods are skipped; a missing or wrong token is <c>CSRF_INVALID</c>.
/// </summary>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter, IOrderedFilter
{
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>The cookie the SPA reads the request token from (not HttpOnly).</summary>
    public const string RequestTokenCookie = "XSRF-TOKEN";

    public int Order => 1000;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = ErrorCodeMapping.ToActionResult(context.HttpContext, new Error(ErrorCode.CsrfInvalid));
        }
    }
}
