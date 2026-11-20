using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Domain.Inspections;

/// <summary>The vehicle data the rule engine needs.</summary>
public sealed record InspectionInput(
    VehicleType VehicleType,
    DisplacementBracket? DisplacementBracket,
    DateOnly ManufactureDate,
    DateOnly OriginalIssueDate,
    DateOnly? LicenceInspectionDate);
