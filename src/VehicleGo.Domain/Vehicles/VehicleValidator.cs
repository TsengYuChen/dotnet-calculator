using VehicleGo.Domain.Common;

namespace VehicleGo.Domain.Vehicles;

/// <summary>
/// Domain validation in design §1.5 order, returning only the first error:
/// VALIDATION_FAILED (field invariants the framework also checks, plus an empty normalised plate) →
/// DATE_IN_FUTURE → ISSUE_BEFORE_MANUFACTURE → BRACKET_TYPE_MISMATCH →
/// LICENCE_DATE_NOT_ALLOWED → LICENCE_DATE_BEFORE_ISSUE → LICENCE_DATE_TOO_FAR.
/// Field names are the camelCase JSON request field names.
/// </summary>
public static class VehicleValidator
{
    public static Result Validate(VehicleDetails details, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(details);

        // 1. Field invariants (VALIDATION_FAILED). The framework's model validation runs first in the
        //    API; these keep the entity valid on its own and cover the empty normalised plate.
        if (details.Plate is null
            || details.Plate.Length > VehicleLimits.PlateMaxLength
            || PlateNormalizer.Normalize(details.Plate).Length == 0)
        {
            return Result.Failure(ErrorCode.ValidationFailed, "plate");
        }

        if (!IsRequiredText(details.Brand))
        {
            return Result.Failure(ErrorCode.ValidationFailed, "brand");
        }

        if (!IsRequiredText(details.Model))
        {
            return Result.Failure(ErrorCode.ValidationFailed, "model");
        }

        if (details.Nickname is { Length: > VehicleLimits.TextMaxLength })
        {
            return Result.Failure(ErrorCode.ValidationFailed, "nickname");
        }

        if (details.Color is { Length: > VehicleLimits.TextMaxLength })
        {
            return Result.Failure(ErrorCode.ValidationFailed, "color");
        }

        if (!Enum.IsDefined(details.VehicleType))
        {
            return Result.Failure(ErrorCode.ValidationFailed, "vehicleType");
        }

        // 2. Future dates (Taipei "today").
        if (details.ManufactureDate > today)
        {
            return Result.Failure(ErrorCode.DateInFuture, "manufactureDate");
        }

        if (details.OriginalIssueDate > today)
        {
            return Result.Failure(ErrorCode.DateInFuture, "originalIssueDate");
        }

        // 3. Issue date before manufacture date.
        if (details.OriginalIssueDate < details.ManufactureDate)
        {
            return Result.Failure(ErrorCode.IssueBeforeManufacture, "originalIssueDate");
        }

        // 4. Bracket matches vehicle type.
        if (!DisplacementBrackets.IsValidFor(details.VehicleType, details.DisplacementBracket))
        {
            return Result.Failure(ErrorCode.BracketTypeMismatch, "displacementBracket");
        }

        // 5. Licence inspection date.
        if (details.LicenceInspectionDate is { } licence)
        {
            if (!DisplacementBrackets.LicenceDateAllowed(details.VehicleType))
            {
                return Result.Failure(ErrorCode.LicenceDateNotAllowed, "licenceInspectionDate");
            }

            if (licence < details.OriginalIssueDate)
            {
                return Result.Failure(ErrorCode.LicenceDateBeforeIssue, "licenceInspectionDate");
            }

            if (licence > LatestLicenceDate(today))
            {
                return Result.Failure(ErrorCode.LicenceDateTooFar, "licenceInspectionDate");
            }
        }

        return Result.Success();
    }

    /// <summary>LICENCE_DATE_TOO_FAR bound: 12/31 of (today's year + 2).</summary>
    public static DateOnly LatestLicenceDate(DateOnly today) => new(today.Year + 2, 12, 31);

    private static bool IsRequiredText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= VehicleLimits.TextMaxLength;
}
