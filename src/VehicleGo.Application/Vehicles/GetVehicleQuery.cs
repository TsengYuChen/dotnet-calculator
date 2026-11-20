using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Common;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;

namespace VehicleGo.Application.Vehicles;

public sealed record GetVehicleQuery(Guid Id);

/// <summary>VEHICLE_NOT_FOUND also for a vehicle in the trash (design §1.4). Reading never logs.</summary>
public sealed class GetVehicleQueryHandler(
    IVehicleQueries queries,
    IRuleTableProvider ruleTableProvider,
    TimeProvider timeProvider)
    : IQueryHandler<GetVehicleQuery, VehicleDto>
{
    public async Task<Result<VehicleDto>> HandleAsync(GetVehicleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var vehicle = await queries.GetActiveAsync(query.Id, cancellationToken);
        if (vehicle is null)
        {
            return Result<VehicleDto>.Failure(ErrorCode.VehicleNotFound);
        }

        var inspections = InspectionCalculator.Calculate(vehicle, TaipeiToday.Get(timeProvider), ruleTableProvider.GetRuleTable());
        return Result<VehicleDto>.Success(VehicleDto.From(vehicle, inspections.Items));
    }
}
