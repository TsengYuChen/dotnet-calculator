using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Domain.Inspections.Rules;

/// <summary>The set of inspection rules passed to the calculator (design §1.2, OQ-1).</summary>
public sealed class RuleTable
{
    /// <summary>規則版本日期 assigned by the author (民國 115/01/01).</summary>
    public static readonly DateOnly RuleVersionDate = new(2026, 1, 1);

    public RuleTable(IEnumerable<InspectionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules.ToList();
    }

    public IReadOnlyList<InspectionRule> Rules { get; }

    public InspectionRule? Find(VehicleType vehicleType, InspectionKind kind) =>
        Rules.FirstOrDefault(r => r.VehicleType == vehicleType && r.Kind == kind);

    /// <summary>A copy without the rule for <paramref name="vehicleType"/> / <paramref name="kind"/> (tests of NO_RULE).</summary>
    public RuleTable Without(VehicleType vehicleType, InspectionKind kind) =>
        new(Rules.Where(r => !(r.VehicleType == vehicleType && r.Kind == kind)));

    /// <summary>All current rules. No transition logic in the first version (OQ-1).</summary>
    public static RuleTable Current() => CurrentTable;

    private static readonly RuleTable CurrentTable = new(
    [
        Periodic(VehicleType.Car, "CAR"),
        Periodic(VehicleType.HeavyMc, "HMC"),
        Exhaust(VehicleType.FuelMc),
        Exhaust(VehicleType.HeavyMc),
        new InspectionRule(
            VehicleType.ElectricMc,
            InspectionKind.Exhaust,
            PeriodCalculation.Exempt,
            LegalCitation.AirPollutionControlAct40,
            RuleVersionDate,
            [new AgeBand(0, null, "APC40-EMC-EXEMPT")],
            LicenceRuleId: null),
    ]);

    /// <summary>道路交通安全規則第 44 條: 未滿 5 年 / 滿 5 年未滿 10 年 / 滿 10 年, plus the licence-date rule ID.</summary>
    private static InspectionRule Periodic(VehicleType type, string code) => new(
        type,
        InspectionKind.Periodic,
        PeriodCalculation.AnnualByAnniversary,
        LegalCitation.RoadTrafficSafetyRules44,
        RuleVersionDate,
        [
            new AgeBand(0, 5, $"RTS44-{code}-AGE0TO5"),
            new AgeBand(5, 10, $"RTS44-{code}-AGE5TO10"),
            new AgeBand(10, null, $"RTS44-{code}-AGE10UP"),
        ],
        LicenceRuleId: $"RTS44-{code}-LICENCE");

    /// <summary>空氣污染防制法第 40 條: fuel motorcycles (incl. heavy) yearly from 5 years old.</summary>
    private static InspectionRule Exhaust(VehicleType type) => new(
        type,
        InspectionKind.Exhaust,
        PeriodCalculation.AnnualByIssueMonth,
        LegalCitation.AirPollutionControlAct40,
        RuleVersionDate,
        [
            new AgeBand(0, 5, "APC40-MC-AGE0TO5"),
            new AgeBand(5, null, "APC40-MC-ANNUAL"),
        ],
        LicenceRuleId: null);
}
