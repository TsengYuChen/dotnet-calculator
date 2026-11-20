using VehicleGo.Domain.Inspections.Rules;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

/// <summary>D-R2 / Technical Plan §3 間隔年數表（道路交通安全規則第 44 條，規則版本日期 2026-01-01）。</summary>
public class FirstInspectionDateTests
{
    private static readonly DateOnly M = D(2020, 1, 15);

    [Theory]
    [InlineData(2020, 1, 15, 5)]  // 同一天
    [InlineData(2020, 8, 20, 5)]  // 未逾 1 年
    [InlineData(2021, 1, 15, 5)]  // 剛好 1 年（含）
    [InlineData(2021, 1, 16, 4)]  // 逾 1 年
    [InlineData(2022, 1, 15, 4)]  // 剛好 2 年
    [InlineData(2022, 1, 16, 3)]  // 逾 2 年
    [InlineData(2023, 1, 15, 3)]  // 剛好 3 年
    [InlineData(2023, 1, 16, 2)]  // 逾 3 年
    [InlineData(2024, 1, 15, 2)]  // 剛好 4 年
    [InlineData(2024, 1, 16, 1)]  // 逾 4 年
    [InlineData(2030, 6, 1, 1)]   // 逾 4 年很多
    public void RTS44_20260101_間隔年數表(int year, int month, int day, int expected)
    {
        var issue = D(year, month, day);

        Assert.Equal(expected, FirstInspectionDate.GapYears(M, issue));
        Assert.Equal(issue.AddYears(expected), FirstInspectionDate.For(M, issue));
    }

    [Fact]
    public void RTS44_20260101_CUJ3_第一次指定檢驗日期為108年8月20日()
    {
        Assert.Equal(D(2019, 8, 20), FirstInspectionDate.For(D(2014, 5, 1), D(2014, 8, 20)));
    }

    [Fact]
    public void RTS44_20260101_CUJ12_第一次指定檢驗日期為117年4月10日()
    {
        Assert.Equal(D(2028, 4, 10), FirstInspectionDate.For(D(2023, 3, 1), D(2023, 4, 10)));
    }

    [Theory]
    [InlineData(2021, 2, 28, 5)]  // 2/29 + 1 年 = 2021-02-28：剛好 1 年
    [InlineData(2021, 3, 1, 4)]   // 逾 1 年
    [InlineData(2024, 2, 29, 2)]  // 剛好 4 年（閏年仍是 2/29）
    [InlineData(2024, 3, 1, 1)]   // 逾 4 年
    public void RTS44_20260101_出廠日期2月29日(int year, int month, int day, int expected)
    {
        Assert.Equal(expected, FirstInspectionDate.GapYears(D(2020, 2, 29), D(year, month, day)));
    }

    [Fact]
    public void RTS44_20260101_原發照日期2月29日加年數_非閏年取2月28日()
    {
        Assert.Equal(D(2025, 2, 28), FirstInspectionDate.For(D(2020, 2, 1), D(2020, 2, 29)));
    }

    [Theory]
    [InlineData(2016, 11, 19, 10)]
    [InlineData(2016, 11, 20, 9)]
    [InlineData(2021, 11, 19, 5)]
    [InlineData(2021, 11, 20, 4)]
    [InlineData(2026, 11, 19, 0)]
    public void RTS44_20260101_車齡滿N年_當天算滿(int year, int month, int day, int expected)
    {
        Assert.Equal(expected, VehicleAge.FullYears(D(year, month, day), Today));
    }

    [Fact]
    public void RTS44_20260101_車齡_出廠2月29日_非閏年2月28日算滿()
    {
        Assert.Equal(1, VehicleAge.FullYears(D(2020, 2, 29), D(2021, 2, 28)));
        Assert.Equal(0, VehicleAge.FullYears(D(2020, 2, 29), D(2021, 2, 27)));
        Assert.Equal(4, VehicleAge.FullYears(D(2020, 2, 29), D(2024, 2, 29)));
        Assert.Equal(3, VehicleAge.FullYears(D(2020, 2, 29), D(2024, 2, 28)));
    }
}
