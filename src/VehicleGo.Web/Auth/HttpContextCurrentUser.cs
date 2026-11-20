using VehicleGo.Application.Abstractions;

namespace VehicleGo.Web.Auth;

/// <summary><see cref="ICurrentUser"/> from the login cookie's claims (no database access).</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(ClaimNames.UserId)?.Value, out var id) ? id : Guid.Empty;

    public string? Email => accessor.HttpContext?.User.FindFirst(ClaimNames.Email)?.Value;
}
