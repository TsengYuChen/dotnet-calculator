using Microsoft.Extensions.Time.Testing;
using VehicleGo.Application.Common;

namespace VehicleGo.UnitTests.Application;

public class TaipeiTodayTests
{
    [Theory]
    [InlineData("2026-11-18T15:59:59Z", 2026, 11, 18)]
    [InlineData("2026-11-18T16:00:00Z", 2026, 11, 19)]
    [InlineData("2026-11-19T15:59:59Z", 2026, 11, 19)]
    [InlineData("2026-12-31T16:00:00Z", 2027, 1, 1)]
    public void 今天是台北時間的日期(string utc, int year, int month, int day)
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(new DateOnly(year, month, day), TaipeiToday.Get(time));
    }

    [Fact]
    public void 不受TimeProvider的本地時區影響()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 17, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);

        Assert.Equal(new DateOnly(2026, 11, 19), TaipeiToday.Get(time));
    }
}
