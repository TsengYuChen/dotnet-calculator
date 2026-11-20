namespace VehicleGo.Web.Logging;

/// <summary>Web log events. Never contain an e-mail address or a plate (Technical Plan §7).</summary>
internal static partial class WebLog
{
    [LoggerMessage(EventId = 2001, EventName = "LOGIN_REJECTED", Level = LogLevel.Warning,
        Message = "Login rejected: subject {Subject}, e-mail hash {EmailHash}, reason {Reason}")]
    public static partial void LoginRejected(ILogger logger, string? subject, string emailHash, string reason);

    [LoggerMessage(EventId = 2002, EventName = "LOGIN_SUCCEEDED", Level = LogLevel.Information,
        Message = "Login succeeded: subject {Subject}, user {UserId}")]
    public static partial void LoginSucceeded(ILogger logger, string subject, Guid userId);

    [LoggerMessage(EventId = 2003, EventName = "ALLOWLIST_REVOKED", Level = LogLevel.Warning,
        Message = "Login cookie rejected, the e-mail is no longer on ALLOWED_EMAILS: subject {Subject}")]
    public static partial void AllowlistRevoked(ILogger logger, string? subject);

    [LoggerMessage(EventId = 2004, EventName = "ALLOWED_EMAILS_EMPTY", Level = LogLevel.Warning,
        Message = "ALLOWED_EMAILS is empty: every login will be rejected.")]
    public static partial void AllowedEmailsEmpty(ILogger logger);

    [LoggerMessage(EventId = 2005, EventName = "OIDC_REMOTE_FAILURE", Level = LogLevel.Warning,
        Message = "Google sign-in failed: {Error}")]
    public static partial void OidcRemoteFailure(ILogger logger, string error);
}
