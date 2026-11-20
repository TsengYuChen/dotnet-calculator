using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Me;
using VehicleGo.Web.Contracts;
using VehicleGo.Web.Security;

namespace VehicleGo.Web.Controllers;

[Route("api/me")]
public sealed class MeController : ApiControllerBase
{
    /// <summary>
    /// The current user, and a fresh CSRF request token in the <c>XSRF-TOKEN</c> cookie (readable by the
    /// SPA, so not HttpOnly). The token is bound to the signed-in user, so the SPA calls this after login.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetMeQuery, MeDto> handler,
        [FromServices] IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(AntiforgeryFilter.RequestTokenCookie, tokens.RequestToken ?? string.Empty, new CookieOptions
        {
            HttpOnly = false,
            Path = "/",
            SameSite = SameSiteMode.Strict,
            Secure = Request.IsHttps,
            IsEssential = true,
        });

        return Respond(await handler.HandleAsync(new GetMeQuery(), cancellationToken), Ok);
    }

    [HttpPatch("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        UpdatePreferencesRequest request,
        [FromServices] ICommandHandler<UpdatePreferencesCommand, MeDto> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Respond(await handler.HandleAsync(new UpdatePreferencesCommand(request.Calendar!.Value), cancellationToken), Ok);
    }
}
