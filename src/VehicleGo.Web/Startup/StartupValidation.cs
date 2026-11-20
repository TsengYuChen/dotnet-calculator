using VehicleGo.Application.Common;
using VehicleGo.Infrastructure.Crypto;
using VehicleGo.Web.Auth;
using VehicleGo.Web.Logging;

namespace VehicleGo.Web.Startup;

/// <summary>
/// Start-up checks (design §2.5), read from <see cref="IConfiguration"/> so tests can supply in-memory values:
/// <c>MASTER_KEY</c> (Base64, 32 bytes), the Asia/Taipei time zone, <c>GOOGLE_CLIENT_ID</c> /
/// <c>GOOGLE_CLIENT_SECRET</c>; an empty <c>ALLOWED_EMAILS</c> only warns (every login is rejected).
/// </summary>
public static class StartupValidation
{
    public const string GoogleClientIdKey = "GOOGLE_CLIENT_ID";
    public const string GoogleClientSecretKey = "GOOGLE_CLIENT_SECRET";

    public static void Validate(IConfiguration configuration, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        if (!MasterKey.TryParse(configuration[MasterKey.ConfigurationKey], out _))
        {
            throw new InvalidOperationException(
                $"{MasterKey.ConfigurationKey} must be 32 random bytes in Base64 (generate one with: openssl rand -base64 32).");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TaipeiToday.TimeZoneId, out _))
        {
            throw new InvalidOperationException($"The {TaipeiToday.TimeZoneId} time zone is not available (install tzdata).");
        }

        foreach (var key in new[] { GoogleClientIdKey, GoogleClientSecretKey })
        {
            if (string.IsNullOrWhiteSpace(configuration[key]))
            {
                throw new InvalidOperationException($"{key} is not configured.");
            }
        }

        if (EnvironmentAllowlistProvider.Parse(configuration[EnvironmentAllowlistProvider.ConfigurationKey]).Count == 0)
        {
            WebLog.AllowedEmailsEmpty(logger);
        }
    }
}
