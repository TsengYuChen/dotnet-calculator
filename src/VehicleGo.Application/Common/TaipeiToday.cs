namespace VehicleGo.Application.Common;

/// <summary>"Today" is the Asia/Taipei calendar date (G6), always derived from <see cref="TimeProvider"/>.</summary>
public static class TaipeiToday
{
    public const string TimeZoneId = "Asia/Taipei";

    private static readonly Lazy<TimeZoneInfo> Taipei = new(() => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));

    public static DateOnly Get(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var taipeiNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Taipei.Value);
        return DateOnly.FromDateTime(taipeiNow.DateTime);
    }
}
