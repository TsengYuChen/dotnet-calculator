using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;

namespace VehicleGo.Application.Vehicles;

public sealed record RestoreVehicleCommand(Guid Id);

/// <summary>
/// Design §2.3: no <see cref="IUnitOfWork"/>. VEHICLE_NOT_FOUND (also after 30 days in the trash) →
/// VEHICLE_NOT_IN_TRASH → restore → save without version check. Plates are unique including the trash,
/// so restoring cannot collide.
/// </summary>
public sealed class RestoreVehicleCommandHandler(IVehicleRepository vehicles, TimeProvider timeProvider)
    : ICommandHandler<RestoreVehicleCommand>
{
    public async Task<Result> HandleAsync(RestoreVehicleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = timeProvider.GetUtcNow();

        var vehicle = await vehicles.GetForOwnerAsync(command.Id, includeTrashed: true, VehicleSupport.TrashCutoff(now), cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(ErrorCode.VehicleNotFound);
        }

        var restored = vehicle.Restore(now);
        return restored.IsFailure ? restored : await vehicles.SaveAsync(vehicle, null, cancellationToken);
    }
}
