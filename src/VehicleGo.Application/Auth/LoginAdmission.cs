using VehicleGo.Application.Abstractions;

namespace VehicleGo.Application.Auth;

/// <summary>
/// The allowlist check (G1), called by the OIDC sign-in event and by the cookie validation event on
/// every request. E-mails are compared after trimming and lower-casing both sides.
/// </summary>
public sealed class LoginAdmission(IAllowlistProvider allowlist)
{
    /// <summary>Sign-in: the e-mail must be verified by Google and on the allowlist.</summary>
    public bool Admit(string? email, bool emailVerified) => emailVerified && IsAllowed(email);

    /// <summary>Every request: the cookie's e-mail must still be on the allowlist.</summary>
    public bool IsAllowed(string? email)
    {
        var normalized = Normalize(email);
        if (normalized.Length == 0)
        {
            return false;
        }

        return allowlist.GetAllowedEmails().Any(allowed => Normalize(allowed) == normalized);
    }

    private static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
