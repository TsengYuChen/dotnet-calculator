using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleGo.Web.Auth;

namespace VehicleGo.Web.Controllers;

[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Starts Google sign-in; an invalid <c>returnUrl</c> becomes <c>/</c>.</summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl) =>
        Challenge(new AuthenticationProperties { RedirectUri = ReturnUrlValidator.Sanitize(returnUrl) }, AuthenticationSetup.OidcScheme);

    /// <summary>Signs out of the cookie only (not Google).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthenticationSetup.CookieScheme);
        return NoContent();
    }
}
