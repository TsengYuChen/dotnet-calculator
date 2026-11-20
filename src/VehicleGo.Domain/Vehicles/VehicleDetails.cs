namespace VehicleGo.Domain.Vehicles;

/// <summary>User-editable vehicle data (create / full update).</summary>
public sealed record VehicleDetails(
    string Plate,
    string Brand,
    string Model,
    string? Nickname,
    string? Color,
    VehicleType VehicleType,
    DisplacementBracket? DisplacementBracket,
    DateOnly ManufactureDate,
    DateOnly OriginalIssueDate,
    DateOnly? LicenceInspectionDate);
