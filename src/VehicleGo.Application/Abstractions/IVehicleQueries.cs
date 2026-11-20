using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Abstractions;

/// <summary>
/// Read-side queries (D-T2), all limited to the current user. They only load data; inspection
/// windows are calculated by the handlers with the Domain rule engine.
/// </summary>
public interface IVehicleQueries
{
    /// <summary>The current user's vehicles that are not in the trash (unordered).</summary>
    Task<IReadOnlyList<Vehicle>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>The vehicle when it exists, belongs to the current user and is not in the trash.</summary>
    Task<Vehicle?> GetActiveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The current user's trashed vehicles with <c>deleted_at &gt; cutoff</c>.</summary>
    Task<IReadOnlyList<Vehicle>> ListTrashAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
