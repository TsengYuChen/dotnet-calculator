using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Common;

/// <summary>Fixed error codes (design §1.5). The JSON / DB code is the attribute value.</summary>
public enum ErrorCode
{
    [JsonStringEnumMemberName("VALIDATION_FAILED")] ValidationFailed,
    [JsonStringEnumMemberName("ISSUE_BEFORE_MANUFACTURE")] IssueBeforeManufacture,
    [JsonStringEnumMemberName("DATE_IN_FUTURE")] DateInFuture,
    [JsonStringEnumMemberName("BRACKET_TYPE_MISMATCH")] BracketTypeMismatch,
    [JsonStringEnumMemberName("LICENCE_DATE_NOT_ALLOWED")] LicenceDateNotAllowed,
    [JsonStringEnumMemberName("LICENCE_DATE_BEFORE_ISSUE")] LicenceDateBeforeIssue,
    [JsonStringEnumMemberName("LICENCE_DATE_TOO_FAR")] LicenceDateTooFar,
    [JsonStringEnumMemberName("CSRF_INVALID")] CsrfInvalid,
    [JsonStringEnumMemberName("VEHICLE_NOT_FOUND")] VehicleNotFound,
    [JsonStringEnumMemberName("PLATE_EXISTS_ACTIVE")] PlateExistsActive,
    [JsonStringEnumMemberName("PLATE_EXISTS_IN_TRASH")] PlateExistsInTrash,
    [JsonStringEnumMemberName("VEHICLE_LIMIT_REACHED")] VehicleLimitReached,
    [JsonStringEnumMemberName("VEHICLE_MODIFIED")] VehicleModified,
    [JsonStringEnumMemberName("VEHICLE_IN_TRASH")] VehicleInTrash,
    [JsonStringEnumMemberName("VEHICLE_NOT_IN_TRASH")] VehicleNotInTrash,
}
