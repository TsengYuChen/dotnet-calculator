using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;

namespace VehicleGo.Application.Vehicles;

public sealed record TrashVehicleCommand(Guid Id);

/// <summary>
/// Design §2.3: no <see cref="IUnitOfWork"/> (one vehicle, no plate / limit impact).
/// VEHICLE_NOT_FOUND → VEHICLE_IN_TRASH → trash → save without version check.
/// </summary>
public sealed class TrashVehicleCommandHandler(IVehicleRepository vehicles, TimeProvider timeProvider)
    : ICommandHandler<TrashVehicleCommand>
{
    public async Task<Result> HandleAsync(TrashVehicleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = timeProvider.GetUtcNow();

        var vehicle = await vehicles.GetForOwnerAsync(command.Id, includeTrashed: true, VehicleSupport.TrashCutoff(now), cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(ErrorCode.VehicleNotFound);
        }

        var trashed = vehicle.Trash(now);
        return trashed.IsFailure ? trashed : await vehicles.SaveAsync(vehicle, null, cancellationToken);
    }
}
