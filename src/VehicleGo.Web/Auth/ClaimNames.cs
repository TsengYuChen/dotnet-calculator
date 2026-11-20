namespace VehicleGo.Web.Auth;

/// <summary>The three claims of the login cookie (Technical Plan §4): <c>users.id</c>, Google <c>sub</c>, e-mail.</summary>
public static class ClaimNames
{
    /// <summary>Internal user ID (<c>users.id</c>).</summary>
    public const string UserId = "uid";

    /// <summary>Google subject identifier.</summary>
    public const string Subject = "sub";

    public const string Email = "email";

    /// <summary>Google's id_token claim (read at sign-in only, never stored in the cookie).</summary>
    public const string EmailVerified = "email_verified";
}
