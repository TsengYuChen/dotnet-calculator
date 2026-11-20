using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

/// <summary>道路交通安全規則第 44 條（規則版本日期 2026-01-01）：有填行照上的指定檢驗日期。</summary>
public class InspectionCalculatorLicenceDateTests
{
    private const InspectionKind P = InspectionKind.Periodic;

    private static InspectionResult CarCalc(DateOnly manufacture, DateOnly issue, DateOnly licence, DateOnly? today = null) =>
        Calc(VehicleType.Car, DisplacementBracket.Car1201To1800, manufacture, issue, licence, today);

    [Fact]
    public void RTS44_20260101_CUJ4_填了指定檢驗日期_以行照為準_判定不一致()
    {
        // 出廠 108/06/01、原發照 108/07/15，指定檢驗日期 116/03/10。
        var result = CarCalc(D(2019, 6, 1), D(2019, 7, 15), D(2027, 3, 10));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2027, 2, 10), D(2027, 4, 10),
            InspectionBasis.Licence, estimated: false, "RTS44-CAR-LICENCE");
        Assert.True(result.Diagnostics.LicenceDateMismatch);
        Assert.Equal(D(2027, 7, 15), result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void RTS44_20260101_CUJ4b_指定檢驗日期已經過了_自動往後推一年()
    {
        var result = CarCalc(D(2019, 6, 1), D(2019, 7, 15), D(2026, 3, 10));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2027, 2, 10), D(2027, 4, 10),
            InspectionBasis.Licence, estimated: false, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public void RTS44_20260101_CUJ4c_滿10年填了下一次的指定檢驗日期_補上前一年的推算期間_判定一致()
    {
        // 出廠 103/05/01、原發照 103/08/20，指定檢驗日期 116/08/20 → L − 6 個月 = 116/02/20 的推算期間。
        var result = CarCalc(D(2014, 5, 1), D(2014, 8, 20), D(2027, 8, 20));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2027, 1, 20), D(2027, 3, 20),
            InspectionBasis.Licence, estimated: true, "RTS44-CAR-LICENCE");
        Assert.False(result.Diagnostics.LicenceDateMismatch);
        Assert.Equal(D(2027, 8, 20), result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void RTS44_20260101_填了好幾年前的指定檢驗日期_往後推到下一個期間()
    {
        var result = CarCalc(D(2019, 6, 1), D(2019, 7, 15), D(2020, 3, 10));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2027, 2, 10), D(2027, 4, 10),
            InspectionBasis.Licence, estimated: false, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public void RTS44_20260101_未滿5年也用指定檢驗日期()
    {
        var result = CarCalc(D(2023, 3, 1), D(2023, 4, 10), D(2027, 1, 5));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2026, 12, 5), D(2027, 2, 5),
            InspectionBasis.Licence, estimated: false, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public void RTS44_20260101_滿10年有填指定檢驗日期_推算期間是LICENCE且estimated()
    {
        // L = 2027-05-10：L − 1 年 = 2026-05-10（已滿 10 年）+ 6 個月 = 2026-11-10，期間 10/10–12/10 含今天。
        var result = CarCalc(D(2010, 1, 1), D(2010, 5, 10), D(2027, 5, 10));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.InWindow, D(2026, 10, 10), D(2026, 12, 10),
            InspectionBasis.Licence, estimated: true, "RTS44-CAR-LICENCE");
        Assert.False(result.Diagnostics.LicenceDateMismatch);
    }

    [Fact]
    public void RTS44_20260101_指定檢驗日期當天未滿10年_不補推算期間()
    {
        // 出廠 2017-06-01、L = 2027-03-10：L − 1 年 = 2026-03-10 當天 8 年，不補推算期間。
        var result = CarCalc(D(2017, 6, 1), D(2017, 6, 10), D(2027, 3, 10));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2027, 2, 10), D(2027, 4, 10),
            InspectionBasis.Licence, estimated: false, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public void RTS44_20260101_指定檢驗日期2月29日_非閏年取2月28日()
    {
        var result = CarCalc(D(2019, 1, 1), D(2019, 2, 20), D(2024, 2, 29));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2027, 1, 28), D(2027, 3, 28),
            InspectionBasis.Licence, estimated: false, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public void RTS44_20260101_指定檢驗日期2月29日_閏年週年日仍是2月29日()
    {
        // 從 L 直接加年數，不從前一年（2/28）累加。需要 2028 年成為下一次，所以「今天」另設為 2027-12-01。
        var result = CarCalc(D(2019, 1, 1), D(2019, 2, 20), D(2024, 2, 29), today: D(2027, 12, 1));

        AssertWindow(Assert.Single(result.Items), P, InspectionStatus.Upcoming, D(2028, 1, 29), D(2028, 3, 29),
            InspectionBasis.Licence, estimated: false, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public void RTS44_20260101_不一致判斷_月日和同年度公式日期相同_一致()
    {
        // 公式日期每年 7/15（第一次 2024-07-15）；填 2027-07-15。
        var result = CarCalc(D(2019, 6, 1), D(2019, 7, 15), D(2027, 7, 15));

        Assert.False(result.Diagnostics.LicenceDateMismatch);
        Assert.Equal(D(2027, 7, 15), result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void RTS44_20260101_不一致判斷_滿10年時和公式日期加6個月的月日相同_一致()
    {
        // CUJ 3 的車：公式日期 8/20，2026-08-20 已滿 10 年 → 推算 2027-02-20；填 2027-02-20 算一致。
        var result = CarCalc(D(2014, 5, 1), D(2014, 8, 20), D(2027, 2, 20));

        Assert.False(result.Diagnostics.LicenceDateMismatch);
    }

    [Fact]
    public void RTS44_20260101_不一致判斷_未滿10年時和公式日期加6個月的月日相同_不一致()
    {
        // 公式日期 7/15（2026-07-15 當天車齡 7 年）；填 2027-01-15 → 不一致。
        var result = CarCalc(D(2019, 6, 1), D(2019, 7, 15), D(2027, 1, 15));

        Assert.True(result.Diagnostics.LicenceDateMismatch);
        Assert.Equal(D(2027, 7, 15), result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void RTS44_20260101_不一致判斷_指定檢驗日期早於第一次公式日期_月日相同_一致()
    {
        // 第一次公式日期 2028-04-10；填 2027-04-10（早於第一次）→ 改和第一次公式日期比月日 → 一致。
        var result = CarCalc(D(2023, 3, 1), D(2023, 4, 10), D(2027, 4, 10));

        Assert.False(result.Diagnostics.LicenceDateMismatch);
        Assert.Equal(D(2028, 4, 10), result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void RTS44_20260101_不一致判斷_指定檢驗日期早於第一次公式日期_月日不同_不一致()
    {
        var result = CarCalc(D(2023, 3, 1), D(2023, 4, 10), D(2027, 1, 5));

        Assert.True(result.Diagnostics.LicenceDateMismatch);
        Assert.Equal(D(2028, 4, 10), result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void RTS44_20260101_沒填指定檢驗日期_沒有不一致診斷()
    {
        var result = Calc(VehicleType.Car, DisplacementBracket.Car1201To1800, D(2019, 6, 1), D(2019, 7, 15));

        Assert.False(result.Diagnostics.LicenceDateMismatch);
        Assert.Null(result.Diagnostics.FormulaDate);
    }

    [Fact]
    public void RTS44_20260101_大型重機填了指定檢驗日期_規則ID是HMC_LICENCE()
    {
        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.McGt550, D(2014, 5, 1), D(2014, 8, 20), D(2027, 8, 20));

        AssertWindow(Item(result, P), P, InspectionStatus.Upcoming, D(2027, 1, 20), D(2027, 3, 20),
            InspectionBasis.Licence, estimated: true, "RTS44-HMC-LICENCE");
    }
}
