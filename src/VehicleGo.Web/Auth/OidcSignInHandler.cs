using System.Security.Claims;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Auth;
using VehicleGo.Web.Logging;

namespace VehicleGo.Web.Auth;

/// <summary>Admitted with the user ID and the cookie principal (three claims), or rejected.</summary>
public sealed record OidcSignInResult(bool Admitted, Guid UserId, ClaimsPrincipal? Principal)
{
    public static readonly OidcSignInResult Rejected = new(false, Guid.Empty, null);
}

/// <summary>
/// Google sign-in (Technical Plan §4): <see cref="LoginAdmission"/> (allowlist + <c>email_verified</c>),
/// then <see cref="EnsureUserCommand"/>. Called by <c>OnTokenValidated</c> and by the integration tests'
/// test login endpoint. A rejection creates no user and logs only the first 8 hex digits of the e-mail hash.
/// </summary>
public sealed class OidcSignInHandler(
    LoginAdmission admission,
    ICommandHandler<EnsureUserCommand, Guid> ensureUser,
    IEmailProtector emailProtector,
    ILogger<OidcSignInHandler> logger)
{
    public const string ReasonNotAllowed = "NOT_ALLOWED";
    public const string ReasonEmailNotVerified = "EMAIL_NOT_VERIFIED";
    public const string ReasonMissingClaims = "MISSING_CLAIMS";

    public async Task<OidcSignInResult> HandleAsync(string? subject, string? email, bool emailVerified, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            return Reject(subject, email, ReasonMissingClaims);
        }

        if (!admission.IsAllowed(email))
        {
            return Reject(subject, email, ReasonNotAllowed);
        }

        if (!admission.Admit(email, emailVerified))
        {
            return Reject(subject, email, ReasonEmailNotVerified);
        }

        var result = await ensureUser.HandleAsync(new EnsureUserCommand(subject, email), cancellationToken);
        if (result.IsFailure)
        {
            return OidcSignInResult.Rejected;
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, result.Value.ToString()),
                new Claim(ClaimNames.Subject, subject),
                new Claim(ClaimNames.Email, email.Trim()),
            ],
            AuthenticationSetup.CookieScheme,
            ClaimNames.Subject,
            roleType: null);
        WebLog.LoginSucceeded(logger, subject, result.Value);
        return new OidcSignInResult(true, result.Value, new ClaimsPrincipal(identity));
    }

    private OidcSignInResult Reject(string? subject, string? email, string reason)
    {
        var hash = string.IsNullOrWhiteSpace(email)
            ? "none"
            : Convert.ToHexStringLower(emailProtector.ComputeLookupHash(email))[..8];
        WebLog.LoginRejected(logger, subject, hash, reason);
        return OidcSignInResult.Rejected;
    }
}
