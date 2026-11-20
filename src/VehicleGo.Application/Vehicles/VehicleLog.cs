using System.Globalization;
using Microsoft.Extensions.Logging;
using VehicleGo.Application.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

/// <summary>
/// Structured log events written by the create / update handlers after the transaction commits
/// (design §2.3 step 8). Never contains the plate (Technical Plan §7).
/// </summary>
internal static partial class VehicleLog
{
    [LoggerMessage(
        EventId = 1001,
        EventName = "LICENCE_DATE_MISMATCH",
        Level = LogLevel.Warning,
        Message = "LICENCE_DATE_MISMATCH vehicleId={VehicleId} vehicleType={VehicleType} manufactureDate={ManufactureDate} " +
                  "originalIssueDate={OriginalIssueDate} licenceInspectionDate={LicenceInspectionDate} formulaDate={FormulaDate}")]
    private static partial void LicenceDateMismatch(
        ILogger logger,
        Guid vehicleId,
        string vehicleType,
        string manufactureDate,
        string originalIssueDate,
        string licenceInspectionDate,
        string formulaDate);

    [LoggerMessage(
        EventId = 1002,
        EventName = "INSPECTION_UNCOMPUTABLE",
        Level = LogLevel.Warning,
        Message = "INSPECTION_UNCOMPUTABLE vehicleId={VehicleId} kind={Kind} reason={Reason}")]
    private static partial void InspectionUncomputable(ILogger logger, Guid vehicleId, string kind, string reason);

    /// <summary>Writes LICENCE_DATE_MISMATCH / INSPECTION_UNCOMPUTABLE from the rule engine's diagnostics.</summary>
    public static void WriteDiagnostics(ILogger logger, Vehicle vehicle, InspectionDiagnostics diagnostics)
    {
        if (diagnostics.LicenceDateMismatch && vehicle.LicenceInspectionDate is { } licence)
        {
            LicenceDateMismatch(
                logger,
                vehicle.Id,
                Codes.Of(vehicle.VehicleType),
                Format(vehicle.ManufactureDate),
                Format(vehicle.OriginalIssueDate),
                Format(licence),
                diagnostics.FormulaDate is { } formula ? Format(formula) : string.Empty);
        }

        foreach (var item in diagnostics.Uncomputable)
        {
            InspectionUncomputable(logger, vehicle.Id, Codes.Of(item.Kind), Codes.Of(item.Reason));
        }
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
