namespace VehicleGo.Domain.Inspections;

/// <summary>Result of <see cref="InspectionCalculator.Calculate(InspectionInput, DateOnly, Rules.RuleTable)"/>.</summary>
public sealed record InspectionResult(IReadOnlyList<InspectionItem> Items, InspectionDiagnostics Diagnostics);
