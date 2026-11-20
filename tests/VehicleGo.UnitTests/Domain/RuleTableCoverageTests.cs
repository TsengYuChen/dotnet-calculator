using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Inspections.Rules;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

/// <summary>規則表（design §1.2）：車種 × 檢驗種類 × 車齡區間沒有重疊、沒有缺口。規則版本日期 2026-01-01。</summary>
public class RuleTableCoverageTests
{
    private static readonly (VehicleType Type, InspectionKind Kind)[] Applicable =
    [
        (VehicleType.Car, InspectionKind.Periodic),
        (VehicleType.FuelMc, InspectionKind.Exhaust),
        (VehicleType.HeavyMc, InspectionKind.Periodic),
        (VehicleType.HeavyMc, InspectionKind.Exhaust),
        (VehicleType.ElectricMc, InspectionKind.Exhaust),
    ];

    public static TheoryData<VehicleType, InspectionKind> ApplicableKinds
    {
        get
        {
            var data = new TheoryData<VehicleType, InspectionKind>();
            foreach (var (type, kind) in Applicable)
            {
                data.Add(type, kind);
            }
            return data;
        }
    }

    [Fact]
    public void RTS44_APC40_20260101_適用的檢驗種類()
    {
        Assert.Equal([InspectionKind.Periodic], InspectionCalculator.KindsFor(VehicleType.Car));
        Assert.Equal([InspectionKind.Exhaust], InspectionCalculator.KindsFor(VehicleType.FuelMc));
        Assert.Equal([InspectionKind.Periodic, InspectionKind.Exhaust], InspectionCalculator.KindsFor(VehicleType.HeavyMc));
        Assert.Equal([InspectionKind.Exhaust], InspectionCalculator.KindsFor(VehicleType.ElectricMc));
    }

    [Fact]
    public void RTS44_APC40_20260101_每個車種和檢驗種類剛好一條規則_沒有多餘的規則()
    {
        var rules = RuleTable.Current().Rules;
        var expected = Applicable.ToHashSet();

        Assert.Equal(expected.Count, rules.Count);
        Assert.Equal(expected, rules.Select(r => (r.VehicleType, r.Kind)).ToHashSet());
    }

    [Theory]
    [MemberData(nameof(ApplicableKinds))]
    public void RTS44_APC40_20260101_車齡區間從0年開始_沒有重疊也沒有缺口(VehicleType type, InspectionKind kind)
    {
        var rule = RuleTable.Current().Find(type, kind);
        Assert.NotNull(rule);

        var bands = rule.AgeBands;
        Assert.Equal(0, bands[0].FromYears);
        for (var i = 1; i < bands.Count; i++)
        {
            Assert.Equal(bands[i - 1].ToYears, bands[i].FromYears);
        }
        Assert.Null(bands[^1].ToYears);

        for (var age = 0; age <= 60; age++)
        {
            Assert.Single(bands, b => b.Contains(age));
        }
    }

    [Fact]
    public void RTS44_APC40_20260101_規則ID和車齡區間()
    {
        var table = RuleTable.Current();
        string Id(VehicleType t, InspectionKind k, int age) => table.Find(t, k)!.RuleIdFor(age);

        foreach (var (t, code) in new[] { (VehicleType.Car, "CAR"), (VehicleType.HeavyMc, "HMC") })
        {
            Assert.Equal($"RTS44-{code}-AGE0TO5", Id(t, InspectionKind.Periodic, 0));
            Assert.Equal($"RTS44-{code}-AGE0TO5", Id(t, InspectionKind.Periodic, 4));
            Assert.Equal($"RTS44-{code}-AGE5TO10", Id(t, InspectionKind.Periodic, 5));
            Assert.Equal($"RTS44-{code}-AGE5TO10", Id(t, InspectionKind.Periodic, 9));
            Assert.Equal($"RTS44-{code}-AGE10UP", Id(t, InspectionKind.Periodic, 10));
            Assert.Equal($"RTS44-{code}-AGE10UP", Id(t, InspectionKind.Periodic, 40));
            Assert.Equal($"RTS44-{code}-LICENCE", table.Find(t, InspectionKind.Periodic)!.LicenceRuleId);
        }

        foreach (var t in new[] { VehicleType.FuelMc, VehicleType.HeavyMc })
        {
            Assert.Equal("APC40-MC-AGE0TO5", Id(t, InspectionKind.Exhaust, 4));
            Assert.Equal("APC40-MC-ANNUAL", Id(t, InspectionKind.Exhaust, 5));
            Assert.Equal("APC40-MC-ANNUAL", Id(t, InspectionKind.Exhaust, 40));
            Assert.Null(table.Find(t, InspectionKind.Exhaust)!.LicenceRuleId);
        }

        Assert.Equal("APC40-EMC-EXEMPT", Id(VehicleType.ElectricMc, InspectionKind.Exhaust, 0));
        Assert.Equal("APC40-EMC-EXEMPT", Id(VehicleType.ElectricMc, InspectionKind.Exhaust, 40));
    }

    [Fact]
    public void RTS44_APC40_20260101_算法法規出處和規則版本日期()
    {
        foreach (var rule in RuleTable.Current().Rules)
        {
            Assert.Equal(new DateOnly(2026, 1, 1), rule.EffectiveFrom);

            var (calculation, citation) = (rule.VehicleType, rule.Kind) switch
            {
                (VehicleType.ElectricMc, InspectionKind.Exhaust) => (PeriodCalculation.Exempt, LegalCitation.AirPollutionControlAct40),
                (_, InspectionKind.Exhaust) => (PeriodCalculation.AnnualByIssueMonth, LegalCitation.AirPollutionControlAct40),
                _ => (PeriodCalculation.AnnualByAnniversary, LegalCitation.RoadTrafficSafetyRules44),
            };
            Assert.Equal(calculation, rule.Calculation);
            Assert.Equal(citation, rule.Citation);
            Assert.All(rule.AgeBands, b => Assert.StartsWith(citation.Abbreviation + "-", b.RuleId));
        }
    }

    public static TheoryData<int> Ages
    {
        get
        {
            var data = new TheoryData<int>();
            for (var age = 0; age <= 15; age++)
            {
                data.Add(age);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Ages))]
    public void RTS44_20260101_沒填指定檢驗日期依今天車齡選規則_有填一律LICENCE(int age)
    {
        // 出廠日期 = 今天往前 age 年；原發照日期 = 出廠日期。
        var manufacture = Today.AddYears(-age);
        var expectedFormula = age switch { < 5 => "AGE0TO5", < 10 => "AGE5TO10", _ => "AGE10UP" };

        foreach (var (type, bracket, code) in new[]
                 {
                     (VehicleType.Car, (DisplacementBracket?)DisplacementBracket.Car601To1200, "CAR"),
                     (VehicleType.HeavyMc, (DisplacementBracket?)DisplacementBracket.Mc251To550, "HMC"),
                 })
        {
            var formula = Item(Calc(type, bracket, manufacture, manufacture), InspectionKind.Periodic);
            Assert.Equal($"RTS44-{code}-{expectedFormula}", formula.RuleId);
            Assert.Equal(InspectionBasis.Formula, formula.Basis);

            var licence = Item(Calc(type, bracket, manufacture, manufacture, licence: D(2027, 3, 10)), InspectionKind.Periodic);
            Assert.Equal($"RTS44-{code}-LICENCE", licence.RuleId);
            Assert.Equal(InspectionBasis.Licence, licence.Basis);
        }

        var exhaust = Item(Calc(VehicleType.FuelMc, DisplacementBracket.Mc51To250, manufacture, manufacture), InspectionKind.Exhaust);
        Assert.Equal(age < 5 ? "APC40-MC-AGE0TO5" : "APC40-MC-ANNUAL", exhaust.RuleId);
    }
}
