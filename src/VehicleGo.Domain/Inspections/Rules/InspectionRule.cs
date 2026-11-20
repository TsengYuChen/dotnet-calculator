using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Domain.Inspections.Rules;

/// <summary>Rule ID used while the vehicle's age (full years, today) is in [<paramref name="FromYears"/>, <paramref name="ToYears"/>).</summary>
/// <param name="ToYears">Exclusive upper bound; null = no upper bound.</param>
public sealed record AgeBand(int FromYears, int? ToYears, string RuleId)
{
    public bool Contains(int ageYears) => ageYears >= FromYears && (ToYears is null || ageYears < ToYears);
}

/// <summary>One row of the rule table (design §1.2) for a vehicle type and inspection kind.</summary>
/// <param name="AgeBands">Rule IDs by today's age when no licence date is used.</param>
/// <param name="LicenceRuleId">Rule ID when the 指定檢驗日期 is entered (periodic only).</param>
/// <param name="EffectiveFrom">規則版本日期 (author-assigned 2026-01-01, not the actual amendment date).</param>
public sealed record InspectionRule(
    VehicleType VehicleType,
    InspectionKind Kind,
    PeriodCalculation Calculation,
    LegalCitation Citation,
    DateOnly EffectiveFrom,
    IReadOnlyList<AgeBand> AgeBands,
    string? LicenceRuleId)
{
    /// <summary>The rule ID for a vehicle that is <paramref name="ageYears"/> full years old today.</summary>
    public string RuleIdFor(int ageYears)
    {
        foreach (var band in AgeBands)
        {
            if (band.Contains(ageYears))
            {
                return band.RuleId;
            }
        }

        // Ages below zero cannot occur for validated data; fall back to the youngest band.
        return AgeBands[0].RuleId;
    }
}
