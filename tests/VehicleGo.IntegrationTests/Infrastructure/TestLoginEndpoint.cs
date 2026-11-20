using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using VehicleGo.Web.Auth;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only sign-in: <c>/test/login?sub=&amp;email=&amp;emailVerified=</c> calls the real
/// <see cref="OidcSignInHandler"/> with simulated Google claims and, when admitted, issues the real login
/// cookie (204); rejected → 403. Added by <see cref="VehicleGoFactory"/> as a startup filter, so the
/// production Web project contains no such endpoint.
/// </summary>
public sealed class TestLoginEndpoint : IStartupFilter
{
    public const string Path = "/test/login";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Map(Path, branch => branch.Run(async context =>
        {
            var query = context.Request.Query;
            var handler = context.RequestServices.GetRequiredService<OidcSignInHandler>();
            var result = await handler.HandleAsync(
                query["sub"],
                query["email"],
                string.Equals(query["emailVerified"], "true", StringComparison.OrdinalIgnoreCase),
                context.RequestAborted);

            if (!result.Admitted || result.Principal is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await context.SignInAsync(AuthenticationSetup.CookieScheme, result.Principal);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        }));
        next(app);
    };
}
