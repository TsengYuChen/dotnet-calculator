using Microsoft.Extensions.Time.Testing;
using VehicleGo.Domain.Vehicles;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Application;

/// <summary>
/// Fakes wired together for handler tests. "Now" is 2026-11-19T02:00Z, i.e. 10:00 on 2026-11-19 in Taipei
/// (the same "today" as the rule tests).
/// </summary>
internal sealed class AppTestHarness
{
    public static readonly DateTimeOffset Now = new(2026, 11, 19, 2, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 11, 19);
    public static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    public AppTestHarness()
    {
        Time = new FakeTimeProvider(Now);
        CurrentUser = new FakeCurrentUser();
        Repository = new FakeVehicleRepository(Store, CurrentUser, Log);
        Queries = new FakeVehicleQueries(Store, CurrentUser);
        UnitOfWork = new FakeUnitOfWork(Log);
    }

    public CallLog Log { get; } = new();
    public FakeVehicleStore Store { get; } = new();
    public FakeTimeProvider Time { get; }
    public FakeCurrentUser CurrentUser { get; }
    public FakeVehicleRepository Repository { get; }
    public FakeVehicleQueries Queries { get; }
    public FakeUnitOfWork UnitOfWork { get; }
    public FakeRuleTableProvider Rules { get; } = new();

    public CapturingLogger<T> Logger<T>() => new(Log);

    public static DateOnly D(int year, int month, int day) => new(year, month, day);

    public static VehicleDetails Details(
        string plate = "ABC-1234",
        VehicleType type = VehicleType.Car,
        DisplacementBracket? bracket = DisplacementBracket.Car1201To1800,
        DateOnly? manufacture = null,
        DateOnly? issue = null,
        DateOnly? licence = null,
        string brand = "Toyota",
        string model = "Altis",
        string? nickname = null,
        string? color = null) =>
        new(plate, brand, model, nickname, color, type, bracket,
            manufacture ?? D(2019, 6, 1), issue ?? D(2019, 7, 15), licence);

    /// <summary>Stores a vehicle as if it had been saved earlier (with a version).</summary>
    public Vehicle Seed(VehicleDetails details, Guid? owner = null, DateTimeOffset? trashedAt = null, Guid? id = null)
    {
        var vehicle = Vehicle.Create(id ?? Guid.NewGuid(), owner ?? CurrentUser.UserId, details, Today, Now.AddDays(-60)).Value;
        if (trashedAt is { } at)
        {
            Assert.True(vehicle.Trash(at).IsSuccess);
        }

        Store.BumpVersion(vehicle);
        Store.Vehicles.Add(vehicle);
        return vehicle;
    }

    public void SeedMany(int active, int trashed)
    {
        for (var i = 0; i < active; i++)
        {
            Seed(Details(plate: $"ACT-{i:D4}"));
        }

        for (var i = 0; i < trashed; i++)
        {
            Seed(Details(plate: $"TRS-{i:D4}"), trashedAt: Now.AddDays(-1));
        }
    }
}
