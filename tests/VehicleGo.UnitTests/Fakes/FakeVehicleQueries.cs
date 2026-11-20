using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Fakes;

/// <summary>In-memory <see cref="IVehicleQueries"/>; returns vehicles in insertion order (unsorted).</summary>
public sealed class FakeVehicleQueries(FakeVehicleStore store, ICurrentUser currentUser) : IVehicleQueries
{
    public DateTimeOffset? LastCutoff { get; private set; }

    public Task<IReadOnlyList<Vehicle>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Vehicle>>(store.Visible(currentUser.UserId, null).Where(v => !v.IsTrashed).ToList());

    public Task<Vehicle?> GetActiveAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(store.Visible(currentUser.UserId, null).FirstOrDefault(v => v.Id == id && !v.IsTrashed));

    public Task<IReadOnlyList<Vehicle>> ListTrashAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        LastCutoff = cutoff;
        return Task.FromResult<IReadOnlyList<Vehicle>>(store.Visible(currentUser.UserId, cutoff).Where(v => v.IsTrashed).ToList());
    }
}
