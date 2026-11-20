using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

/// <summary>A vehicle with its inspections (design §1.4).</summary>
public sealed record VehicleDto(
    Guid Id,
    uint Version,
    string Plate,
    string Brand,
    string Model,
    string? Nickname,
    string? Color,
    VehicleType VehicleType,
    DisplacementBracket? DisplacementBracket,
    DateOnly ManufactureDate,
    DateOnly OriginalIssueDate,
    DateOnly? LicenceInspectionDate,
    IReadOnlyList<InspectionDto> Inspections)
{
    public static VehicleDto From(Vehicle vehicle, IEnumerable<InspectionItem> inspections)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(inspections);
        return new(
            vehicle.Id,
            vehicle.Version,
            vehicle.Plate,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Nickname,
            vehicle.Color,
            vehicle.VehicleType,
            vehicle.DisplacementBracket,
            vehicle.ManufactureDate,
            vehicle.OriginalIssueDate,
            vehicle.LicenceInspectionDate,
            inspections.Select(InspectionDto.From).ToArray());
    }
}
