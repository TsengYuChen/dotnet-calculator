using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Common;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

/// <summary>
/// <paramref name="Sort"/> is the raw <c>sort</c> query value: <c>deadline</c> (default when null)
/// or <c>manufactureDate</c>; anything else is VALIDATION_FAILED (design §1.7).
/// </summary>
public sealed record ListVehiclesQuery(string? Sort = null)
{
    public const string SortByDeadline = "deadline";
    public const string SortByManufactureDate = "manufactureDate";
}

/// <summary>
/// The current user's active vehicles with inspections, sorted in memory (G5, at most 50 vehicles):
/// <list type="bullet">
/// <item><c>deadline</c>: the earliest deadline among the vehicle's inspections, earliest first; vehicles
/// with no deadline (EXEMPT / UNCOMPUTABLE only) last; ties by plate.</item>
/// <item><c>manufactureDate</c>: oldest first; ties by plate.</item>
/// </list>
/// Plates compare by their normalised form; the vehicle ID is the final tie-breaker so the order is stable.
/// </summary>
public sealed class ListVehiclesQueryHandler(
    IVehicleQueries queries,
    IRuleTableProvider ruleTableProvider,
    TimeProvider timeProvider)
    : IQueryHandler<ListVehiclesQuery, IReadOnlyList<VehicleDto>>
{
    public async Task<Result<IReadOnlyList<VehicleDto>>> HandleAsync(ListVehiclesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sort = query.Sort ?? ListVehiclesQuery.SortByDeadline;
        if (sort is not (ListVehiclesQuery.SortByDeadline or ListVehiclesQuery.SortByManufactureDate))
        {
            return Result<IReadOnlyList<VehicleDto>>.Failure(ErrorCode.ValidationFailed, "sort");
        }

        var vehicles = await queries.ListActiveAsync(cancellationToken);
        var today = TaipeiToday.Get(timeProvider);
        var rules = ruleTableProvider.GetRuleTable();

        var rows = vehicles
            .Select(v => (Vehicle: v, Items: InspectionCalculator.Calculate(v, today, rules).Items))
            .ToList();

        var ordered = sort == ListVehiclesQuery.SortByDeadline
            ? rows.Select(r => (Row: r, Deadline: EarliestDeadline(r.Items)))
                .OrderBy(x => x.Deadline is null)
                .ThenBy(x => x.Deadline)
                .ThenBy(x => x.Row.Vehicle.NormalizedPlate, StringComparer.Ordinal)
                .ThenBy(x => x.Row.Vehicle.Id)
                .Select(x => x.Row)
            : rows.OrderBy(r => r.Vehicle.ManufactureDate)
                .ThenBy(r => r.Vehicle.NormalizedPlate, StringComparer.Ordinal)
                .ThenBy(r => r.Vehicle.Id);

        IReadOnlyList<VehicleDto> result = ordered.Select(r => VehicleDto.From(r.Vehicle, r.Items)).ToList();
        return Result<IReadOnlyList<VehicleDto>>.Success(result);
    }

    private static DateOnly? EarliestDeadline(IEnumerable<InspectionItem> items) =>
        items.Where(i => i.Deadline is not null).Min(i => i.Deadline);
}
