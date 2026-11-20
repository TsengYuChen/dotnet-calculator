namespace VehicleGo.Application.Abstractions;

/// <summary>The signed-in user, read from the login cookie claims (no database access).</summary>
public interface ICurrentUser
{
    /// <summary><c>users.id</c>; <see cref="Guid.Empty"/> when nobody is signed in.</summary>
    Guid UserId { get; }

    /// <summary>The e-mail claim of the cookie; null when nobody is signed in.</summary>
    string? Email { get; }
}
