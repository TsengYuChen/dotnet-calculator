using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;

namespace VehicleGo.Application.Vehicles;

/// <summary>Permanent delete; only for a vehicle in the trash.</summary>
public sealed record DeleteVehicleCommand(Guid Id);

/// <summary>Design §2.3: same checks as restore, then delete; no <see cref="IUnitOfWork"/>.</summary>
public sealed class DeleteVehicleCommandHandler(IVehicleRepository vehicles, TimeProvider timeProvider)
    : ICommandHandler<DeleteVehicleCommand>
{
    public async Task<Result> HandleAsync(DeleteVehicleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = timeProvider.GetUtcNow();

        var vehicle = await vehicles.GetForOwnerAsync(command.Id, includeTrashed: true, VehicleSupport.TrashCutoff(now), cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(ErrorCode.VehicleNotFound);
        }

        if (!vehicle.IsTrashed)
        {
            return Result.Failure(ErrorCode.VehicleNotInTrash);
        }

        return await vehicles.DeleteAsync(vehicle, cancellationToken);
    }
}
