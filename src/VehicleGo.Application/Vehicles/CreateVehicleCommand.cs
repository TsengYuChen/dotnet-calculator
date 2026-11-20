using Microsoft.Extensions.Logging;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Common;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

public sealed record CreateVehicleCommand(VehicleDetails Details);

/// <summary>Design §2.3 create flow; the steps after validation run in one transaction.</summary>
public sealed class CreateVehicleCommandHandler(
    IVehicleRepository vehicles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IRuleTableProvider ruleTableProvider,
    TimeProvider timeProvider,
    ILogger<CreateVehicleCommandHandler> logger)
    : ICommandHandler<CreateVehicleCommand, VehicleDto>
{
    public async Task<Result<VehicleDto>> HandleAsync(CreateVehicleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = timeProvider.GetUtcNow();
        var today = TaipeiToday.Get(timeProvider);

        // 1. Domain validation, before the transaction.
        var created = Vehicle.Create(Guid.CreateVersion7(now), currentUser.UserId, command.Details, today, now);
        if (created.IsFailure)
        {
            return Result<VehicleDto>.Failure(created.Error);
        }

        var vehicle = created.Value;
        var cutoff = VehicleSupport.TrashCutoff(now);

        // 2–7. One transaction.
        var saved = await unitOfWork.ExecuteInTransactionAsync<Vehicle>(async ct =>
        {
            await vehicles.LockOwnerAsync(ct);
            await vehicles.PurgeExpiredTrashAsync(cutoff, ct);

            if (await VehicleSupport.CheckPlateAsync(vehicles, vehicle.NormalizedPlate, null, cutoff, ct) is { } plateError)
            {
                return Result<Vehicle>.Failure(plateError);
            }

            var (active, trash) = await vehicles.CountForOwnerAsync(ct);
            if (active + trash >= VehicleLimits.MaxVehiclesPerOwner)
            {
                return Result<Vehicle>.Failure(VehicleSupport.LimitReached(active, trash));
            }

            var added = await vehicles.AddAsync(vehicle, ct);
            return added.IsSuccess ? Result<Vehicle>.Success(vehicle) : Result<Vehicle>.Failure(added.Error);
        }, cancellationToken);

        if (saved.IsFailure)
        {
            return Result<VehicleDto>.Failure(saved.Error);
        }

        // 8. After commit: diagnostics → log.
        var inspections = InspectionCalculator.Calculate(vehicle, today, ruleTableProvider.GetRuleTable());
        VehicleLog.WriteDiagnostics(logger, vehicle, inspections.Diagnostics);
        return Result<VehicleDto>.Success(VehicleDto.From(vehicle, inspections.Items));
    }
}
