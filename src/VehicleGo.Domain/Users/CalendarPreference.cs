using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Users;

/// <summary>Calendar used for date input and display (G6).</summary>
public enum CalendarPreference
{
    /// <summary>民國年（預設）</summary>
    [JsonStringEnumMemberName("ROC")] Roc,

    /// <summary>西元年</summary>
    [JsonStringEnumMemberName("GREGORIAN")] Gregorian,
}
