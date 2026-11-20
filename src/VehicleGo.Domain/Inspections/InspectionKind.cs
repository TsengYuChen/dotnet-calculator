using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Inspections;

public enum InspectionKind
{
    /// <summary>定期檢驗（道路交通安全規則第 44 條）</summary>
    [JsonStringEnumMemberName("PERIODIC")] Periodic,

    /// <summary>排氣定檢（空氣污染防制法第 40 條）</summary>
    [JsonStringEnumMemberName("EXHAUST")] Exhaust,
}
