using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Fakes;

/// <summary>In-memory vehicle table shared by <see cref="FakeVehicleRepository"/> and <see cref="FakeVehicleQueries"/>.</summary>
public sealed class FakeVehicleStore
{
    private uint _nextVersion = 100;

    public List<Vehicle> Vehicles { get; } = [];

    /// <summary>Simulates PostgreSQL <c>xmin</c>: every insert / update gets a new version.</summary>
    public void BumpVersion(Vehicle vehicle) => SetVersion(vehicle, ++_nextVersion);

    public static void SetVersion(Vehicle vehicle, uint version) =>
        typeof(Vehicle).GetProperty(nameof(Vehicle.Version))!.SetValue(vehicle, version);

    /// <summary>Visible to <paramref name="ownerId"/>: owned and not trashed since the cutoff or earlier.</summary>
    public IEnumerable<Vehicle> Visible(Guid ownerId, DateTimeOffset? cutoff) =>
        Vehicles.Where(v => v.OwnerId == ownerId && !(v.DeletedAt is { } d && cutoff is { } c && d <= c));
}
