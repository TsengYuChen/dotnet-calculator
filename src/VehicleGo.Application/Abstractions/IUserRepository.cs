using VehicleGo.Domain.Users;

namespace VehicleGo.Application.Abstractions;

/// <summary>Users (D-T2). Reading the calendar preference for <c>/api/me</c> is the D-T2 exception.</summary>
public interface IUserRepository
{
    /// <summary>
    /// Creates the user for <paramref name="googleSub"/> or updates its e-mail; concurrent first logins
    /// create only one row. Returns <c>users.id</c> (written into the login cookie).
    /// </summary>
    Task<Guid> UpsertOnLoginAsync(string googleSub, byte[] emailEncrypted, byte[] emailLookupHash, CancellationToken cancellationToken);

    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CalendarPreference> GetCalendarPreferenceAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(User user, CancellationToken cancellationToken);
}
