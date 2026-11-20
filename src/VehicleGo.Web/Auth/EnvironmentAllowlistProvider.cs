using VehicleGo.Application.Abstractions;

namespace VehicleGo.Web.Auth;

/// <summary>
/// <c>ALLOWED_EMAILS</c>: comma-separated, each entry trimmed and lower-cased, empty entries dropped.
/// Read from <see cref="IConfiguration"/> on every call, so the allowlist follows the configuration.
/// </summary>
public sealed class EnvironmentAllowlistProvider(IConfiguration configuration) : IAllowlistProvider
{
    public const string ConfigurationKey = "ALLOWED_EMAILS";

    public IReadOnlyCollection<string> GetAllowedEmails() => Parse(configuration[ConfigurationKey]);

    public static IReadOnlyCollection<string> Parse(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
