using Microsoft.EntityFrameworkCore;
using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Users;

namespace VehicleGo.Infrastructure.Persistence.Repositories;

/// <summary><see cref="IUserRepository"/>; the login upsert is raw SQL (EF Core has no upsert).</summary>
public sealed class UserRepository(AppDbContext db, TimeProvider timeProvider) : IUserRepository
{
    public async Task<Guid> UpsertOnLoginAsync(string googleSub, byte[] emailEncrypted, byte[] emailLookupHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSub);
        ArgumentNullException.ThrowIfNull(emailEncrypted);
        ArgumentNullException.ThrowIfNull(emailLookupHash);

        var now = timeProvider.GetUtcNow();
        var newId = Guid.CreateVersion7(now);

        // ToListAsync, not SingleAsync: SingleAsync composes a subquery around the INSERT, which PostgreSQL rejects.
        // calendar_preference is left out so new users get the column default ('ROC').
        var ids = await db.Database.SqlQuery<Guid>($"""
            INSERT INTO users (id, google_sub, email_encrypted, email_lookup_hash, created_at, updated_at)
            VALUES ({newId}, {googleSub}, {emailEncrypted}, {emailLookupHash}, {now}, {now})
            ON CONFLICT (google_sub) DO UPDATE
                SET email_encrypted = EXCLUDED.email_encrypted,
                    email_lookup_hash = EXCLUDED.email_lookup_hash,
                    updated_at = EXCLUDED.updated_at
            RETURNING id AS "Value"
            """).ToListAsync(cancellationToken);
        return ids.Single();
    }

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<CalendarPreference> GetCalendarPreferenceAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == id).Select(u => u.CalendarPreference).SingleAsync(cancellationToken);

    public async Task SaveAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (db.Entry(user).State == EntityState.Detached)
        {
            db.Users.Update(user);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
