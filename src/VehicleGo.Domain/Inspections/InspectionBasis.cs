using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Inspections;

public enum InspectionBasis
{
    /// <summary>公式推算</summary>
    [JsonStringEnumMemberName("FORMULA")] Formula,

    /// <summary>行照指定檢驗日期</summary>
    [JsonStringEnumMemberName("LICENCE")] Licence,
}
