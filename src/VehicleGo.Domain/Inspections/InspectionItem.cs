namespace VehicleGo.Domain.Inspections;

/// <summary>One inspection line as exposed by the API (no server-only diagnostics).</summary>
/// <param name="EarliestDate">期間起日 (inclusive); null for EXEMPT / UNCOMPUTABLE.</param>
/// <param name="Deadline">最後期限 (inclusive); null for EXEMPT / UNCOMPUTABLE.</param>
/// <param name="RuleId">e.g. <c>RTS44-CAR-AGE10UP</c>; null for UNCOMPUTABLE.</param>
/// <param name="Estimated">True for the "指定檢驗日期 + 6 個月" estimated window.</param>
public sealed record InspectionItem(
    InspectionKind Kind,
    InspectionStatus Status,
    DateOnly? EarliestDate,
    DateOnly? Deadline,
    string? RuleId,
    InspectionBasis Basis,
    bool Estimated);
