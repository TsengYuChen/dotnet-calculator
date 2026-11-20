using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IVehicleRepository"/> limited to <see cref="ICurrentUser.UserId"/> (like the owner
/// query filter). Each call is recorded in the shared <see cref="CallLog"/>; write results can be overridden
/// to simulate the database fallbacks (version conflict, unique index).
/// </summary>
public sealed class FakeVehicleRepository(FakeVehicleStore store, ICurrentUser currentUser, CallLog log) : IVehicleRepository
{
    public Result? AddResult { get; set; }
    public Result? SaveResult { get; set; }

    public DateTimeOffset? LastCutoff { get; private set; }
    public uint? LastExpectedVersion { get; private set; }

    private Guid Owner => currentUser.UserId;

    public Task<Vehicle?> GetForOwnerAsync(Guid id, bool includeTrashed, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        log.Add($"Get(includeTrashed={includeTrashed})");
        LastCutoff = cutoff;
        var vehicle = store.Visible(Owner, cutoff).FirstOrDefault(v => v.Id == id);
        return Task.FromResult(vehicle is { IsTrashed: true } && !includeTrashed ? null : vehicle);
    }

    public Task<Vehicle?> FindByNormalizedPlateAsync(string normalizedPlate, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        log.Add("FindByPlate");
        LastCutoff = cutoff;
        return Task.FromResult(store.Visible(Owner, cutoff).FirstOrDefault(v => v.NormalizedPlate == normalizedPlate));
    }

    public Task LockOwnerAsync(CancellationToken cancellationToken)
    {
        log.Add("Lock");
        return Task.CompletedTask;
    }

    public Task<(int Active, int Trash)> CountForOwnerAsync(CancellationToken cancellationToken)
    {
        log.Add("Count");
        var mine = store.Visible(Owner, null).ToList();
        return Task.FromResult((mine.Count(v => !v.IsTrashed), mine.Count(v => v.IsTrashed)));
    }

    public Task PurgeExpiredTrashAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        log.Add("Purge");
        LastCutoff = cutoff;
        store.Vehicles.RemoveAll(v => v.OwnerId == Owner && v.DeletedAt is { } d && d <= cutoff);
        return Task.CompletedTask;
    }

    public Task<Result> AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        log.Add("Add");
        if (AddResult is { IsFailure: true })
        {
            return Task.FromResult(AddResult);
        }

        store.BumpVersion(vehicle);
        store.Vehicles.Add(vehicle);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SaveAsync(Vehicle vehicle, uint? expectedVersion, CancellationToken cancellationToken)
    {
        log.Add(expectedVersion is null ? "Save" : $"Save(version={expectedVersion})");
        LastExpectedVersion = expectedVersion;
        if (SaveResult is { IsFailure: true })
        {
            return Task.FromResult(SaveResult);
        }

        if (expectedVersion is { } expected && expected != vehicle.Version)
        {
            return Task.FromResult(Result.Failure(ErrorCode.VehicleModified));
        }

        store.BumpVersion(vehicle);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        log.Add("Delete");
        store.Vehicles.Remove(vehicle);
        return Task.FromResult(Result.Success());
    }
}
