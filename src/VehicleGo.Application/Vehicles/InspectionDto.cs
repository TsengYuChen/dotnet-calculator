using VehicleGo.Domain.Inspections;

namespace VehicleGo.Application.Vehicles;

/// <summary>One inspection line of <see cref="VehicleDto"/> (design §1.4); never contains diagnostics.</summary>
public sealed record InspectionDto(
    InspectionKind Kind,
    InspectionStatus Status,
    DateOnly? EarliestDate,
    DateOnly? Deadline,
    string? RuleId,
    bool Estimated,
    InspectionBasis Basis)
{
    public static InspectionDto From(InspectionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new(item.Kind, item.Status, item.EarliestDate, item.Deadline, item.RuleId, item.Estimated, item.Basis);
    }
}
