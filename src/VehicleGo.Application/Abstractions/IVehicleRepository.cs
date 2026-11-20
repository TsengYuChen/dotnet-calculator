using VehicleGo.Domain.Common;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Abstractions;

/// <summary>
/// Write-side access to the current user's vehicles (D-T2, design §2.3). Every method is limited to
/// the current user (owner query filter / <see cref="ICurrentUser"/>); write methods save changes
/// themselves and translate database errors into <see cref="Result"/>.
/// </summary>
public interface IVehicleRepository
{
    /// <summary>
    /// The vehicle, or null when it does not exist, belongs to someone else, is trashed and
    /// <paramref name="includeTrashed"/> is false, or has been in the trash since <paramref name="cutoff"/> or earlier.
    /// </summary>
    Task<Vehicle?> GetForOwnerAsync(Guid id, bool includeTrashed, DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Active or trashed (trashed after <paramref name="cutoff"/>) vehicle with this normalised plate.</summary>
    Task<Vehicle?> FindByNormalizedPlateAsync(string normalizedPlate, DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Locks the current user's row in the open transaction, so one user's writes run one at a time.</summary>
    Task LockOwnerAsync(CancellationToken cancellationToken);

    /// <summary>Active and trashed vehicle counts of the current user.</summary>
    Task<(int Active, int Trash)> CountForOwnerAsync(CancellationToken cancellationToken);

    /// <summary>Permanently deletes the current user's vehicles with <c>deleted_at &lt;= cutoff</c>.</summary>
    Task PurgeExpiredTrashAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    Task<Result> AddAsync(Vehicle vehicle, CancellationToken cancellationToken);

    /// <summary>Saves changes; <paramref name="expectedVersion"/> null means no version check.</summary>
    Task<Result> SaveAsync(Vehicle vehicle, uint? expectedVersion, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Vehicle vehicle, CancellationToken cancellationToken);
}
