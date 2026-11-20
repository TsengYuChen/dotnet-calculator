using System.ComponentModel.DataAnnotations;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Web.Contracts;

/// <summary>
/// <c>VehicleInput</c> (design §1.4). Required value types are nullable + <see cref="RequiredAttribute"/>,
/// so a missing value is a model validation error instead of a silent default.
/// </summary>
public class VehicleInput
{
    [Required]
    [StringLength(VehicleLimits.PlateMaxLength)]
    public string? Plate { get; init; }

    [Required]
    [StringLength(VehicleLimits.TextMaxLength)]
    public string? Brand { get; init; }

    [Required]
    [StringLength(VehicleLimits.TextMaxLength)]
    public string? Model { get; init; }

    [StringLength(VehicleLimits.TextMaxLength)]
    public string? Nickname { get; init; }

    [StringLength(VehicleLimits.TextMaxLength)]
    public string? Color { get; init; }

    [Required]
    public VehicleType? VehicleType { get; init; }

    public DisplacementBracket? DisplacementBracket { get; init; }

    [Required]
    public DateOnly? ManufactureDate { get; init; }

    [Required]
    public DateOnly? OriginalIssueDate { get; init; }

    public DateOnly? LicenceInspectionDate { get; init; }

    /// <summary>Only called after model validation passed, so the required values are present.</summary>
    public VehicleDetails ToDetails() => new(
        Plate!,
        Brand!,
        Model!,
        Nickname,
        Color,
        VehicleType!.Value,
        DisplacementBracket,
        ManufactureDate!.Value,
        OriginalIssueDate!.Value,
        LicenceInspectionDate);
}
