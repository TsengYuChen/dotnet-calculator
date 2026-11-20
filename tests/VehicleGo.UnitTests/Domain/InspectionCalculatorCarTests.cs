using VehicleGo.Domain.Inspections;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

/// <summary>道路交通安全規則第 44 條（規則版本日期 2026-01-01）：自用小客車定期檢驗，沒填指定檢驗日期。</summary>
public class InspectionCalculatorCarTests
{
    private const InspectionKind P = InspectionKind.Periodic;

    [Fact]
    public void RTS44_20260101_CUJ3_汽車滿10年_沒填指定檢驗日期_顯示推算期間()
    {
        // 出廠 103/05/01、原發照 103/08/20 → 第一次 108/08/20；115/08/20 的期間已過 → 推算 116/02/20。
        var item = Car(D(2014, 5, 1), D(2014, 8, 20));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2027, 1, 20), D(2027, 3, 20),
            InspectionBasis.Formula, estimated: true, "RTS44-CAR-AGE10UP");
    }

    [Fact]
    public void RTS44_20260101_CUJ12_汽車未滿5年_顯示第一次定期檢驗期間()
    {
        // 出廠 112/03/01、原發照 112/04/10 → 第一次 117/04/10。
        var item = Car(D(2023, 3, 1), D(2023, 4, 10));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2028, 3, 10), D(2028, 5, 10),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE0TO5");
    }

    [Fact]
    public void RTS44_20260101_汽車未滿5年_滿5年前一個月內已進入檢驗期間()
    {
        // 出廠 2021-12-10、發照 2021-12-15 → 第一次 2026-12-15，期間 11/15–2027/01/15 含今天；今天車齡 4 年。
        var item = Car(D(2021, 12, 10), D(2021, 12, 15));

        AssertWindow(item, P, InspectionStatus.InWindow, D(2026, 11, 15), D(2027, 1, 15),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE0TO5");
    }

    [Fact]
    public void RTS44_20260101_汽車今天剛好滿5年_規則AGE5TO10()
    {
        var item = Car(D(2021, 11, 19), D(2021, 11, 19));

        AssertWindow(item, P, InspectionStatus.InWindow, D(2026, 10, 19), D(2026, 12, 19),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車滿5年前一天_規則AGE0TO5()
    {
        var item = Car(D(2021, 11, 20), D(2021, 11, 20));

        AssertWindow(item, P, InspectionStatus.InWindow, D(2026, 10, 20), D(2026, 12, 20),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE0TO5");
    }

    [Fact]
    public void RTS44_20260101_汽車滿5年_每年一次()
    {
        // 出廠 2019-06-01、發照 2019-07-15 → 每年 7/15；2026 的期間已過 → 2027-07-15。
        var item = Car(D(2019, 6, 1), D(2019, 7, 15));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2027, 6, 15), D(2027, 8, 15),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車今天剛好滿10年_規則AGE10UP()
    {
        var item = Car(D(2016, 11, 19), D(2016, 11, 19));

        AssertWindow(item, P, InspectionStatus.InWindow, D(2026, 10, 19), D(2026, 12, 19),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE10UP");
    }

    [Fact]
    public void RTS44_20260101_汽車滿10年前一天_規則AGE5TO10()
    {
        var item = Car(D(2016, 11, 20), D(2016, 11, 20));

        AssertWindow(item, P, InspectionStatus.InWindow, D(2026, 10, 20), D(2026, 12, 20),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車指定檢驗日期當天未滿10年_不產生推算期間()
    {
        // 出廠 2017-06-01：2026-06-10 當天 9 年 → 沒有 2026-12-10 的推算期間，下一個是 2027-06-10。
        var item = Car(D(2017, 6, 1), D(2017, 6, 10));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2027, 5, 10), D(2027, 7, 10),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車指定檢驗日期當天剛好滿10年_產生推算期間()
    {
        // 出廠 2016-06-10、發照同日：2026-06-10 當天剛好滿 10 年 → 推算 2026-12-10，期間 11/10–2027/01/10。
        var item = Car(D(2016, 6, 10), D(2016, 6, 10));

        AssertWindow(item, P, InspectionStatus.InWindow, D(2026, 11, 10), D(2027, 1, 10),
            InspectionBasis.Formula, estimated: true, "RTS44-CAR-AGE10UP");
    }

    [Fact]
    public void RTS44_20260101_汽車滿10年後_推算期間之後回到指定檢驗日期_estimated為false()
    {
        // 出廠 2010-01-01、發照 2010-12-10 → 每年 12/10；2026-06-10 推算期間已過 → 2026-12-10 一般期間（今天在內）。
        var item = Car(D(2010, 1, 1), D(2010, 12, 10));

        AssertWindow(item, P, InspectionStatus.InWindow, D(2026, 11, 10), D(2027, 1, 10),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE10UP");
    }

    [Fact]
    public void RTS44_20260101_汽車2月29日發照_非閏年取2月28日()
    {
        // 發照 2020-02-29、出廠 2020-02-01 → 第一次 2025-02-28；下一次 2027-02-28。
        var item = Car(D(2020, 2, 1), D(2020, 2, 29));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2027, 1, 28), D(2027, 3, 28),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車2月29日發照_閏年仍是2月29日()
    {
        // 週年日從原發照日期直接加年數：2028 年仍是 2/29（不是從 2027-02-28 累加）。
        // 這個案例需要 2028 年的週年日成為下一次，所以「今天」另設為 2027-12-01。
        var item = Car(D(2020, 2, 1), D(2020, 2, 29), today: D(2027, 12, 1));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2028, 1, 29), D(2028, 3, 29),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車3月31日減1個月_非閏年取2月28日()
    {
        // 發照 2021-03-31 → 2027-03-31 的期間起日 = 2027-02-28。
        var item = Car(D(2021, 1, 1), D(2021, 3, 31));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2027, 2, 28), D(2027, 4, 30),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車3月31日減1個月_閏年取2月29日()
    {
        // 發照 2023-03-31 → 第一次 2028-03-31，期間起日 = 2028-02-29。
        var item = Car(D(2023, 1, 1), D(2023, 3, 31));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2028, 2, 29), D(2028, 4, 30),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE0TO5");
    }

    [Fact]
    public void RTS44_20260101_汽車8月31日加6個月_非閏年得2月28日_期間1月28日到3月28日()
    {
        // 發照 2010-08-31 → 2026-08-31 滿 10 年，推算 2027-02-28。
        var item = Car(D(2010, 1, 1), D(2010, 8, 31));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2027, 1, 28), D(2027, 3, 28),
            InspectionBasis.Formula, estimated: true, "RTS44-CAR-AGE10UP");
    }

    [Fact]
    public void RTS44_20260101_汽車8月31日加6個月_閏年得2月29日_期間1月29日到3月29日()
    {
        // 指定檢驗日期 L = 2028-08-31：L − 1 年 = 2027-08-31（已滿 10 年）+ 6 個月 = 2028-02-29。
        var item = Car(D(2010, 1, 1), D(2010, 8, 31), licence: D(2028, 8, 31));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2028, 1, 29), D(2028, 3, 29),
            InspectionBasis.Licence, estimated: true, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public void RTS44_20260101_汽車期間起日和最後期限當天都算檢驗期間內()
    {
        // 發照 2019-12-19 → 2026-12-19 的期間 2026-11-19–2027-01-19：今天 = 期間起日。
        var first = Car(D(2019, 1, 1), D(2019, 12, 19));
        Assert.Equal(InspectionStatus.InWindow, first.Status);
        Assert.Equal(D(2026, 11, 19), first.EarliestDate);

        // 發照 2019-10-19 → 2026-10-19 的期間 2026-09-19–2026-11-19：今天 = 最後期限。
        var last = Car(D(2019, 1, 1), D(2019, 10, 19));
        Assert.Equal(InspectionStatus.InWindow, last.Status);
        Assert.Equal(D(2026, 11, 19), last.Deadline);
    }

    [Fact]
    public void RTS44_20260101_汽車最後期限前一天已過_顯示下一年()
    {
        // 發照 2019-10-18 → 2026 期間到 2026-11-18（昨天）→ 下一個 2027-10-18。
        var item = Car(D(2019, 1, 1), D(2019, 10, 18));

        AssertWindow(item, P, InspectionStatus.Upcoming, D(2027, 9, 18), D(2027, 11, 18),
            InspectionBasis.Formula, estimated: false, "RTS44-CAR-AGE5TO10");
    }

    [Fact]
    public void RTS44_20260101_汽車只有定期檢驗_沒有排氣定檢()
    {
        var result = Calc(VehicleGo.Domain.Vehicles.VehicleType.Car,
            VehicleGo.Domain.Vehicles.DisplacementBracket.CarElectric, D(2014, 5, 1), D(2014, 8, 20));

        Assert.Equal([InspectionKind.Periodic], result.Items.Select(i => i.Kind));
        Assert.Empty(result.Diagnostics.Uncomputable);
        Assert.False(result.Diagnostics.LicenceDateMismatch);
        Assert.Null(result.Diagnostics.FormulaDate);
    }
}
