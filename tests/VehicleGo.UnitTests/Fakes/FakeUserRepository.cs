using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Users;

namespace VehicleGo.UnitTests.Fakes;

/// <summary>In-memory users keyed by Google <c>sub</c>; upsert creates at most one user per sub.</summary>
public sealed class FakeUserRepository(TimeProvider timeProvider) : IUserRepository
{
    public List<User> Users { get; } = [];

    public int SaveCount { get; private set; }

    public Task<Guid> UpsertOnLoginAsync(string googleSub, byte[] emailEncrypted, byte[] emailLookupHash, CancellationToken cancellationToken)
    {
        var existing = Users.FirstOrDefault(u => u.GoogleSub == googleSub);
        if (existing is not null)
        {
            var index = Users.IndexOf(existing);
            var updated = User.Create(existing.Id, googleSub, emailEncrypted, emailLookupHash, existing.CreatedAt);
            updated.ChangeCalendarPreference(existing.CalendarPreference, timeProvider.GetUtcNow());
            Users[index] = updated;
            return Task.FromResult(existing.Id);
        }

        var user = User.Create(Guid.NewGuid(), googleSub, emailEncrypted, emailLookupHash, timeProvider.GetUtcNow());
        Users.Add(user);
        return Task.FromResult(user.Id);
    }

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<CalendarPreference> GetCalendarPreferenceAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.First(u => u.Id == id).CalendarPreference);

    public Task SaveAsync(User user, CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
