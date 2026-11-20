using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Inspections;

public enum InspectionStatus
{
    [JsonStringEnumMemberName("EXEMPT")] Exempt,
    [JsonStringEnumMemberName("UPCOMING")] Upcoming,
    [JsonStringEnumMemberName("IN_WINDOW")] InWindow,
    [JsonStringEnumMemberName("UNCOMPUTABLE")] Uncomputable,
}
