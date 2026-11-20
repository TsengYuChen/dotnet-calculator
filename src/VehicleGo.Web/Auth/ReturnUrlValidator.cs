namespace VehicleGo.Web.Auth;

/// <summary>
/// <c>returnUrl</c> of <c>/api/auth/login</c> (Technical Plan §7): only a site-relative path is accepted
/// (starts with a single <c>/</c>, not <c>//</c> or <c>/\</c>, no control characters); anything else is <c>/</c>.
/// </summary>
public static class ReturnUrlValidator
{
    public const string Default = "/";

    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl[0] != '/')
        {
            return Default;
        }

        if (returnUrl.Length > 1 && returnUrl[1] is '/' or '\\')
        {
            return Default;
        }

        return returnUrl.Any(char.IsControl) ? Default : returnUrl;
    }
}
