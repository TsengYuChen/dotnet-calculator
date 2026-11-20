using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Inspections.Rules;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

/// <summary>UNCOMPUTABLE（規則版本日期 2026-01-01）：NO_RULE 只影響單一檢驗，INVALID_INPUT 影響整台車。</summary>
public class InspectionCalculatorUncomputableTests
{
    private static void AssertUncomputable(InspectionItem item, InspectionKind kind)
    {
        Assert.Equal(kind, item.Kind);
        Assert.Equal(InspectionStatus.Uncomputable, item.Status);
        Assert.Null(item.EarliestDate);
        Assert.Null(item.Deadline);
        Assert.Null(item.RuleId);
        Assert.Equal(InspectionBasis.Formula, item.Basis);
        Assert.False(item.Estimated);
    }

    [Fact]
    public void RTS44_APC40_20260101_大型重機缺排氣定檢規則_定期檢驗照常計算()
    {
        var rules = RuleTable.Current().Without(VehicleType.HeavyMc, InspectionKind.Exhaust);

        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.McGt550, D(2019, 9, 1), D(2019, 11, 25), rules: rules);

        AssertWindow(Item(result, InspectionKind.Periodic), InspectionKind.Periodic, InspectionStatus.InWindow,
            D(2026, 10, 25), D(2026, 12, 25), InspectionBasis.Formula, estimated: false, "RTS44-HMC-AGE5TO10");
        AssertUncomputable(Item(result, InspectionKind.Exhaust), InspectionKind.Exhaust);
        Assert.Equal([new UncomputableDiagnostic(InspectionKind.Exhaust, UncomputableReason.NoRule)],
            result.Diagnostics.Uncomputable);
    }

    [Fact]
    public void RTS44_APC40_20260101_大型重機缺定期檢驗規則_排氣定檢照常計算()
    {
        var rules = RuleTable.Current().Without(VehicleType.HeavyMc, InspectionKind.Periodic);

        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.McGt550, D(2019, 9, 1), D(2019, 11, 25),
            licence: D(2027, 3, 10), rules: rules);

        AssertUncomputable(Item(result, InspectionKind.Periodic), InspectionKind.Periodic);
        Assert.Equal(InspectionStatus.InWindow, Item(result, InspectionKind.Exhaust).Status);
        Assert.Equal([new UncomputableDiagnostic(InspectionKind.Periodic, UncomputableReason.NoRule)],
            result.Diagnostics.Uncomputable);
        Assert.False(result.Diagnostics.LicenceDateMismatch);
    }

    [Fact]
    public void RTS44_20260101_汽車缺定期檢驗規則_NO_RULE()
    {
        var rules = RuleTable.Current().Without(VehicleType.Car, InspectionKind.Periodic);

        var result = Calc(VehicleType.Car, DisplacementBracket.CarLe500, D(2019, 6, 1), D(2019, 7, 15), rules: rules);

        AssertUncomputable(Assert.Single(result.Items), InspectionKind.Periodic);
        Assert.Equal([new UncomputableDiagnostic(InspectionKind.Periodic, UncomputableReason.NoRule)],
            result.Diagnostics.Uncomputable);
    }

    [Fact]
    public void APC40_20260101_電動機車缺免檢規則_NO_RULE()
    {
        var rules = RuleTable.Current().Without(VehicleType.ElectricMc, InspectionKind.Exhaust);

        var result = Calc(VehicleType.ElectricMc, null, D(2021, 3, 1), D(2021, 3, 15), rules: rules);

        AssertUncomputable(Assert.Single(result.Items), InspectionKind.Exhaust);
    }

    [Fact]
    public void RTS44_APC40_20260101_原發照日期早於出廠日期_整台車INVALID_INPUT()
    {
        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.McGt550, D(2019, 11, 25), D(2019, 9, 1));

        Assert.Equal(2, result.Items.Count);
        AssertUncomputable(Item(result, InspectionKind.Periodic), InspectionKind.Periodic);
        AssertUncomputable(Item(result, InspectionKind.Exhaust), InspectionKind.Exhaust);
        Assert.Equal(
            [
                new UncomputableDiagnostic(InspectionKind.Periodic, UncomputableReason.InvalidInput),
                new UncomputableDiagnostic(InspectionKind.Exhaust, UncomputableReason.InvalidInput),
            ],
            result.Diagnostics.Uncomputable);
    }

    [Fact]
    public void APC40_20260101_燃油機車有指定檢驗日期_INVALID_INPUT()
    {
        var result = Calc(VehicleType.FuelMc, DisplacementBracket.Mc51To250, D(2018, 11, 1), D(2018, 11, 5),
            licence: D(2027, 3, 10));

        AssertUncomputable(Assert.Single(result.Items), InspectionKind.Exhaust);
        Assert.Equal(UncomputableReason.InvalidInput, Assert.Single(result.Diagnostics.Uncomputable).Reason);
        Assert.False(result.Diagnostics.LicenceDateMismatch);
        Assert.Null(result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void APC40_20260101_電動機車有指定檢驗日期_INVALID_INPUT()
    {
        var result = Calc(VehicleType.ElectricMc, null, D(2021, 3, 1), D(2021, 3, 15), licence: D(2027, 3, 10));

        AssertUncomputable(Assert.Single(result.Items), InspectionKind.Exhaust);
    }

    public static TheoryData<VehicleType, DisplacementBracket?> MismatchedBrackets => new()
    {
        { VehicleType.Car, DisplacementBracket.Mc51To250 },
        { VehicleType.Car, null },
        { VehicleType.FuelMc, DisplacementBracket.Mc251To550 },
        { VehicleType.FuelMc, DisplacementBracket.CarLe500 },
        { VehicleType.HeavyMc, DisplacementBracket.Mc51To250 },
        { VehicleType.HeavyMc, null },
        { VehicleType.ElectricMc, DisplacementBracket.McLe50 },
        { VehicleType.ElectricMc, DisplacementBracket.CarElectric },
    };

    [Theory]
    [MemberData(nameof(MismatchedBrackets))]
    public void RTS44_APC40_20260101_級距和車種不符_整台車INVALID_INPUT(VehicleType type, DisplacementBracket? bracket)
    {
        var result = Calc(type, bracket, D(2019, 9, 1), D(2019, 11, 25));

        Assert.Equal(InspectionCalculator.KindsFor(type), result.Items.Select(i => i.Kind));
        Assert.All(result.Items, i => AssertUncomputable(i, i.Kind));
        Assert.All(result.Diagnostics.Uncomputable, d => Assert.Equal(UncomputableReason.InvalidInput, d.Reason));
        Assert.Equal(result.Items.Count, result.Diagnostics.Uncomputable.Count);
    }

    [Fact]
    public void RTS44_20260101_指定檢驗日期早於原發照日期_引擎照常計算()
    {
        // LICENCE_DATE_BEFORE_ISSUE 只是輸入驗證；資料庫裡的舊資料只要算得出來就照常顯示。
        var result = Calc(VehicleType.Car, DisplacementBracket.Car1201To1800, D(2019, 6, 1), D(2019, 7, 15),
            licence: D(2019, 3, 10));

        Assert.Equal(InspectionStatus.Upcoming, Assert.Single(result.Items).Status);
        Assert.Empty(result.Diagnostics.Uncomputable);
    }
}
