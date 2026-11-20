namespace VehicleGo.Application.Abstractions;

/// <summary>The login allowlist (<c>ALLOWED_EMAILS</c>), implemented in Web.</summary>
public interface IAllowlistProvider
{
    IReadOnlyCollection<string> GetAllowedEmails();
}
