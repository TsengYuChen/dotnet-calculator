using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Inspections;

/// <summary>Why an inspection is UNCOMPUTABLE (server-only, logged as INSPECTION_UNCOMPUTABLE).</summary>
public enum UncomputableReason
{
    /// <summary>The rule table has no rule for this vehicle type and kind; affects only that kind.</summary>
    [JsonStringEnumMemberName("NO_RULE")] NoRule,

    /// <summary>Stored data cannot be computed; affects every kind of the vehicle.</summary>
    [JsonStringEnumMemberName("INVALID_INPUT")] InvalidInput,
}

public sealed record UncomputableDiagnostic(InspectionKind Kind, UncomputableReason Reason);

/// <summary>Server-only diagnostics of one calculation (never sent to the client).</summary>
/// <param name="Uncomputable">One entry per UNCOMPUTABLE item.</param>
/// <param name="LicenceDateMismatch">The entered 指定檢驗日期 disagrees with the formula (Technical Plan §3).</param>
/// <param name="FormulaDate">The formula date the licence date was compared with (set when a licence date is entered and periodic inspection was computed).</param>
public sealed record InspectionDiagnostics(
    IReadOnlyList<UncomputableDiagnostic> Uncomputable,
    bool LicenceDateMismatch,
    DateOnly? FormulaDate)
{
    public static InspectionDiagnostics None { get; } = new([], false, null);
}
