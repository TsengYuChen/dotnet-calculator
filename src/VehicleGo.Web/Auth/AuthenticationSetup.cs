using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VehicleGo.Application.Auth;
using VehicleGo.Web.Logging;
using VehicleGo.Web.Startup;

namespace VehicleGo.Web.Auth;

/// <summary>
/// Cookie authentication (default scheme and default challenge, so <c>/api</c> answers 401 instead of
/// redirecting) plus Google OIDC (<c>openid email</c> only). The cookie validation event re-checks the
/// allowlist on every request; the OIDC <c>OnTokenValidated</c> event runs <see cref="OidcSignInHandler"/>.
/// </summary>
public static class AuthenticationSetup
{
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string OidcScheme = OpenIdConnectDefaults.AuthenticationScheme;
    public const string CookieName = "vehiclego.auth";
    public const string GoogleAuthority = "https://accounts.google.com";
    public const string NotAllowedPath = "/not-allowed";
    public const string LoginFailedPath = "/login?error=signin";

    /// <summary>Login cookie lifetime (G1): 14 days, renewed while used (sliding expiration).</summary>
    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(14);

    public static IServiceCollection AddVehicleGoAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieScheme;
                options.DefaultChallengeScheme = CookieScheme;
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = CookieLifetime;
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ValidatePrincipalAsync,
                    OnRedirectToLogin = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden),
                };
            })
            .AddOpenIdConnect(OidcScheme, options =>
            {
                options.Authority = GoogleAuthority;
                options.SignInScheme = CookieScheme;
                options.CallbackPath = "/signin-oidc";
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.MapInboundClaims = false;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = TokenValidatedAsync,
                    OnRemoteFailure = RemoteFailure,
                };
            });

        // Client ID / secret are read when the options are first built, so tests can supply them.
        services.AddOptions<OpenIdConnectOptions>(OidcScheme).Configure<IConfiguration>((options, configuration) =>
        {
            options.ClientId = configuration[StartupValidation.GoogleClientIdKey];
            options.ClientSecret = configuration[StartupValidation.GoogleClientSecretKey];
        });

        // Every endpoint requires a login unless it is marked [AllowAnonymous].
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(CookieScheme).RequireAuthenticatedUser().Build());

        services.AddScoped<OidcSignInHandler>();
        return services;
    }

    /// <summary>Every request: the cookie's e-mail must still be on the allowlist; otherwise clear the cookie (→ 401).</summary>
    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var admission = context.HttpContext.RequestServices.GetRequiredService<LoginAdmission>();
        if (admission.IsAllowed(context.Principal?.FindFirst(ClaimNames.Email)?.Value))
        {
            return;
        }

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthenticationSetup));
        WebLog.AllowlistRevoked(logger, context.Principal?.FindFirst(ClaimNames.Subject)?.Value);
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieScheme);
    }

    private static async Task TokenValidatedAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var handler = context.HttpContext.RequestServices.GetRequiredService<OidcSignInHandler>();
        var result = await handler.HandleAsync(
            principal?.FindFirst(ClaimNames.Subject)?.Value,
            principal?.FindFirst(ClaimNames.Email)?.Value,
            string.Equals(principal?.FindFirst(ClaimNames.EmailVerified)?.Value, "true", StringComparison.OrdinalIgnoreCase),
            context.HttpContext.RequestAborted);

        if (!result.Admitted || result.Principal is null)
        {
            // CUJ 2: show "此帳號尚未開放使用"; no user is created and no new redirect to Google.
            context.HandleResponse();
            context.Response.Redirect(NotAllowedPath);
            return;
        }

        context.Principal = result.Principal;
        context.Properties ??= new AuthenticationProperties();
        context.Properties.IsPersistent = true;
    }

    private static Task RemoteFailure(RemoteFailureContext context)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthenticationSetup));
        WebLog.OidcRemoteFailure(logger, context.Failure?.GetType().Name ?? "unknown");
        context.HandleResponse();
        context.Response.Redirect(LoginFailedPath);
        return Task.CompletedTask;
    }

    private static async Task WriteProblemAsync(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = status },
        });
    }
}
