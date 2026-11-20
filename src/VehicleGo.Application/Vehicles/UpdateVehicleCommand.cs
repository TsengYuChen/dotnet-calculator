using Microsoft.Extensions.Logging;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Common;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

/// <summary>Full replacement of a vehicle; <paramref name="Version"/> is the version the client last read.</summary>
public sealed record UpdateVehicleCommand(Guid Id, uint Version, VehicleDetails Details);

/// <summary>
/// Design §2.3 update flow. Check order (§1.4): validation → VEHICLE_NOT_FOUND → VEHICLE_IN_TRASH →
/// VEHICLE_MODIFIED → plate, so an old version with a duplicate plate is VEHICLE_MODIFIED.
/// </summary>
public sealed class UpdateVehicleCommandHandler(
    IVehicleRepository vehicles,
    IUnitOfWork unitOfWork,
    IRuleTableProvider ruleTableProvider,
    TimeProvider timeProvider,
    ILogger<UpdateVehicleCommandHandler> logger)
    : ICommandHandler<UpdateVehicleCommand, VehicleDto>
{
    public async Task<Result<VehicleDto>> HandleAsync(UpdateVehicleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = timeProvider.GetUtcNow();
        var today = TaipeiToday.Get(timeProvider);

        // 1. Domain validation, before the transaction.
        var validation = VehicleValidator.Validate(command.Details, today);
        if (validation.IsFailure)
        {
            return Result<VehicleDto>.Failure(validation.Error);
        }

        var cutoff = VehicleSupport.TrashCutoff(now);
        var normalizedPlate = PlateNormalizer.Normalize(command.Details.Plate);

        // 2–7. One transaction.
        var saved = await unitOfWork.ExecuteInTransactionAsync<Vehicle>(async ct =>
        {
            await vehicles.LockOwnerAsync(ct);
            await vehicles.PurgeExpiredTrashAsync(cutoff, ct);

            var vehicle = await vehicles.GetForOwnerAsync(command.Id, includeTrashed: true, cutoff, ct);
            if (vehicle is null)
            {
                return Result<Vehicle>.Failure(ErrorCode.VehicleNotFound);
            }

            if (vehicle.IsTrashed)
            {
                return Result<Vehicle>.Failure(ErrorCode.VehicleInTrash);
            }

            if (vehicle.Version != command.Version)
            {
                return Result<Vehicle>.Failure(ErrorCode.VehicleModified);
            }

            if (await VehicleSupport.CheckPlateAsync(vehicles, normalizedPlate, vehicle.Id, cutoff, ct) is { } plateError)
            {
                return Result<Vehicle>.Failure(plateError);
            }

            var updated = vehicle.Update(command.Details, today, now);
            if (updated.IsFailure)
            {
                return Result<Vehicle>.Failure(updated.Error);
            }

            var result = await vehicles.SaveAsync(vehicle, command.Version, ct);
            return result.IsSuccess ? Result<Vehicle>.Success(vehicle) : Result<Vehicle>.Failure(result.Error);
        }, cancellationToken);

        if (saved.IsFailure)
        {
            return Result<VehicleDto>.Failure(saved.Error);
        }

        // 8. After commit: diagnostics → log.
        var vehicleSaved = saved.Value;
        var inspections = InspectionCalculator.Calculate(vehicleSaved, today, ruleTableProvider.GetRuleTable());
        VehicleLog.WriteDiagnostics(logger, vehicleSaved, inspections.Diagnostics);
        return Result<VehicleDto>.Success(VehicleDto.From(vehicleSaved, inspections.Items));
    }
}
