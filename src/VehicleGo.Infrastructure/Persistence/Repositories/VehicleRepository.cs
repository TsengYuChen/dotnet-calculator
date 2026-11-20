using Microsoft.EntityFrameworkCore;
using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IVehicleRepository"/> (design §2.4). Queries rely on the <c>Owner</c> filter; trash and count
/// queries switch off only the <c>Deleted</c> filter. Writes save immediately and map database errors.
/// </summary>
public sealed class VehicleRepository(AppDbContext db, ICurrentUser currentUser) : IVehicleRepository
{
    private static readonly string[] DeletedOnly = [AppDbContext.DeletedFilter];

    /// <summary>Owner filter only; trashed vehicles included.</summary>
    private IQueryable<Vehicle> IncludingTrash => db.Vehicles.IgnoreQueryFilters(DeletedOnly);

    public Task<Vehicle?> GetForOwnerAsync(Guid id, bool includeTrashed, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var source = includeTrashed ? IncludingTrash : db.Vehicles;
        return source.FirstOrDefaultAsync(v => v.Id == id && (v.DeletedAt == null || v.DeletedAt > cutoff), cancellationToken);
    }

    public Task<Vehicle?> FindByNormalizedPlateAsync(string normalizedPlate, DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        IncludingTrash.FirstOrDefaultAsync(
            v => v.NormalizedPlate == normalizedPlate && (v.DeletedAt == null || v.DeletedAt > cutoff),
            cancellationToken);

    public async Task LockOwnerAsync(CancellationToken cancellationToken)
    {
        // EF Core has no LINQ form of FOR UPDATE.
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM users WHERE id = {currentUser.UserId} FOR UPDATE", cancellationToken);
    }

    public async Task<(int Active, int Trash)> CountForOwnerAsync(CancellationToken cancellationToken)
    {
        var active = await IncludingTrash.CountAsync(v => v.DeletedAt == null, cancellationToken);
        var trash = await IncludingTrash.CountAsync(v => v.DeletedAt != null, cancellationToken);
        return (active, trash);
    }

    public async Task PurgeExpiredTrashAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        await IncludingTrash
            .Where(v => v.OwnerId == ownerId && v.DeletedAt != null && v.DeletedAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<Result> AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        db.Vehicles.Add(vehicle);
        return PostgresErrorMapper.SaveChangesAsync(db, cancellationToken);
    }

    public Task<Result> SaveAsync(Vehicle vehicle, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        var entry = db.Entry(vehicle);
        if (entry.State == EntityState.Detached)
        {
            throw new InvalidOperationException("Save a vehicle loaded by this repository (tracked), not a new instance.");
        }

        if (expectedVersion is { } version)
        {
            entry.Property(v => v.Version).OriginalValue = version;
        }

        return PostgresErrorMapper.SaveChangesAsync(db, cancellationToken);
    }

    public Task<Result> DeleteAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        db.Vehicles.Remove(vehicle);
        return PostgresErrorMapper.SaveChangesAsync(db, cancellationToken);
    }
}
