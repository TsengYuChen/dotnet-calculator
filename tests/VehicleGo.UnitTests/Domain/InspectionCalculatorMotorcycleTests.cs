using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

/// <summary>
/// 空氣污染防制法第 40 條（排氣定檢）與道路交通安全規則第 44 條（大型重機定期檢驗），規則版本日期 2026-01-01。
/// </summary>
public class InspectionCalculatorMotorcycleTests
{
    private static InspectionItem Exhaust(DateOnly manufacture, DateOnly issue, VehicleType type = VehicleType.FuelMc) =>
        Item(Calc(type, type == VehicleType.FuelMc ? DisplacementBracket.Mc51To250 : DisplacementBracket.Mc251To550,
            manufacture, issue), InspectionKind.Exhaust);

    [Fact]
    public void RTS44_APC40_20260101_CUJ5_大型重機_兩種檢驗都在檢驗期間內()
    {
        // 出廠 108/09/01、原發照 108/11/25，排氣量 551cc 以上。
        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.McGt550, D(2019, 9, 1), D(2019, 11, 25));

        Assert.Equal([InspectionKind.Periodic, InspectionKind.Exhaust], result.Items.Select(i => i.Kind));
        AssertWindow(Item(result, InspectionKind.Periodic), InspectionKind.Periodic, InspectionStatus.InWindow,
            D(2026, 10, 25), D(2026, 12, 25), InspectionBasis.Formula, estimated: false, "RTS44-HMC-AGE5TO10");
        AssertWindow(Item(result, InspectionKind.Exhaust), InspectionKind.Exhaust, InspectionStatus.InWindow,
            D(2026, 10, 1), D(2026, 12, 31), InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void APC40_20260101_CUJ6_電動機車_排氣定檢免檢()
    {
        var result = Calc(VehicleType.ElectricMc, null, D(2021, 3, 1), D(2021, 3, 15));

        var item = Assert.Single(result.Items);
        Assert.Equal(InspectionKind.Exhaust, item.Kind);
        Assert.Equal(InspectionStatus.Exempt, item.Status);
        Assert.Null(item.EarliestDate);
        Assert.Null(item.Deadline);
        Assert.Equal("APC40-EMC-EXEMPT", item.RuleId);
        Assert.Equal(InspectionBasis.Formula, item.Basis);
        Assert.False(item.Estimated);
        Assert.Empty(result.Diagnostics.Uncomputable);
    }

    [Fact]
    public void APC40_20260101_燃油機車只有排氣定檢_沒有定期檢驗()
    {
        var result = Calc(VehicleType.FuelMc, DisplacementBracket.McLe50, D(2018, 11, 1), D(2018, 11, 5));

        var item = Assert.Single(result.Items);
        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.InWindow, D(2026, 10, 1), D(2026, 12, 31),
            InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void APC40_20260101_1月發照_跨年從前一年12月1日開始()
    {
        var item = Exhaust(D(2018, 1, 5), D(2018, 1, 20));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.Upcoming, D(2026, 12, 1), D(2027, 2, 28),
            InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void APC40_20260101_1月發照_閏年最後期限是2月29日()
    {
        // 出廠 2023-01-10 → 出廠 + 5 年 = 2028-01-10；第一個最後期限在它之後的期間是 2027-12-01–2028-02-29。
        var item = Exhaust(D(2023, 1, 10), D(2023, 1, 15));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.Upcoming, D(2027, 12, 1), D(2028, 2, 29),
            InspectionBasis.Formula, estimated: false, "APC40-MC-AGE0TO5");
    }

    [Fact]
    public void APC40_20260101_12月發照_跨年到隔年1月31日()
    {
        var item = Exhaust(D(2018, 12, 3), D(2018, 12, 10));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.InWindow, D(2026, 11, 1), D(2027, 1, 31),
            InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void APC40_20260101_2月發照_最後期限是3月31日()
    {
        var item = Exhaust(D(2018, 2, 1), D(2018, 2, 10));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.Upcoming, D(2027, 1, 1), D(2027, 3, 31),
            InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void APC40_20260101_11月發照_最後期限是12月31日()
    {
        var item = Exhaust(D(2018, 11, 1), D(2018, 11, 30));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.InWindow, D(2026, 10, 1), D(2026, 12, 31),
            InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void APC40_20260101_出廠加5年落在檢驗期間內_這個期間就要驗()
    {
        // 出廠 2021-11-10 → 出廠 + 5 年 = 2026-11-10，在 2026-10-01–2026-12-31 內；今天已滿 5 年。
        var item = Exhaust(D(2021, 11, 10), D(2021, 11, 20));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.InWindow, D(2026, 10, 1), D(2026, 12, 31),
            InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void APC40_20260101_出廠加5年落在未來的檢驗期間內_未滿5年顯示第一個期間()
    {
        // 同設計文件的例子（出廠 3/10、3 月發照）：出廠 2022-03-10 → 2027-03-10 在 2027-02-01–2027-04-30 內。
        var item = Exhaust(D(2022, 3, 10), D(2022, 3, 20));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.Upcoming, D(2027, 2, 1), D(2027, 4, 30),
            InspectionBasis.Formula, estimated: false, "APC40-MC-AGE0TO5");
    }

    [Fact]
    public void APC40_20260101_最後期限剛好等於出廠加5年_這個期間就要驗()
    {
        // 出廠 2021-12-31 → 出廠 + 5 年 = 2026-12-31 = 2026 年期間的最後期限；今天車齡 4 年。
        var item = Exhaust(D(2021, 12, 31), D(2022, 11, 5));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.InWindow, D(2026, 10, 1), D(2026, 12, 31),
            InspectionBasis.Formula, estimated: false, "APC40-MC-AGE0TO5");
    }

    [Fact]
    public void APC40_20260101_最後期限比出廠加5年早一天_從下一年開始()
    {
        // 出廠 2022-01-01 → 出廠 + 5 年 = 2027-01-01，2026 年期間（到 12/31）不用驗。
        var item = Exhaust(D(2022, 1, 1), D(2022, 11, 5));

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.Upcoming, D(2027, 10, 1), D(2027, 12, 31),
            InspectionBasis.Formula, estimated: false, "APC40-MC-AGE0TO5");
    }

    [Fact]
    public void APC40_20260101_大型重機也要排氣定檢()
    {
        var item = Exhaust(D(2018, 1, 5), D(2018, 1, 20), VehicleType.HeavyMc);

        AssertWindow(item, InspectionKind.Exhaust, InspectionStatus.Upcoming, D(2026, 12, 1), D(2027, 2, 28),
            InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void RTS44_APC40_20260101_大型重機有填指定檢驗日期_排氣定檢仍是FORMULA()
    {
        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.Mc251To550, D(2019, 9, 1), D(2019, 11, 25),
            licence: D(2027, 3, 10));

        AssertWindow(Item(result, InspectionKind.Periodic), InspectionKind.Periodic, InspectionStatus.Upcoming,
            D(2027, 2, 10), D(2027, 4, 10), InspectionBasis.Licence, estimated: false, "RTS44-HMC-LICENCE");
        AssertWindow(Item(result, InspectionKind.Exhaust), InspectionKind.Exhaust, InspectionStatus.InWindow,
            D(2026, 10, 1), D(2026, 12, 31), InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void RTS44_20260101_大型重機滿10年_推算期間()
    {
        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.McGt550, D(2014, 5, 1), D(2014, 8, 20));

        AssertWindow(Item(result, InspectionKind.Periodic), InspectionKind.Periodic, InspectionStatus.Upcoming,
            D(2027, 1, 20), D(2027, 3, 20), InspectionBasis.Formula, estimated: true, "RTS44-HMC-AGE10UP");
        AssertWindow(Item(result, InspectionKind.Exhaust), InspectionKind.Exhaust, InspectionStatus.Upcoming,
            D(2027, 7, 1), D(2027, 9, 30), InspectionBasis.Formula, estimated: false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public void RTS44_APC40_20260101_大型重機未滿5年_兩種檢驗都顯示第一個期間()
    {
        var result = Calc(VehicleType.HeavyMc, DisplacementBracket.Mc251To550, D(2023, 3, 1), D(2023, 4, 10));

        AssertWindow(Item(result, InspectionKind.Periodic), InspectionKind.Periodic, InspectionStatus.Upcoming,
            D(2028, 3, 10), D(2028, 5, 10), InspectionBasis.Formula, estimated: false, "RTS44-HMC-AGE0TO5");
        AssertWindow(Item(result, InspectionKind.Exhaust), InspectionKind.Exhaust, InspectionStatus.Upcoming,
            D(2028, 3, 1), D(2028, 5, 31), InspectionBasis.Formula, estimated: false, "APC40-MC-AGE0TO5");
    }
}
