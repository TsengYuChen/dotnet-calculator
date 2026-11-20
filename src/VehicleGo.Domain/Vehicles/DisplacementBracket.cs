using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Vehicles;

/// <summary>Displacement bracket codes (design §1.1, D-R4). Labels and applicable types: <see cref="DisplacementBrackets"/>.</summary>
public enum DisplacementBracket
{
    [JsonStringEnumMemberName("CAR_LE_500")] CarLe500,
    [JsonStringEnumMemberName("CAR_501_600")] Car501To600,
    [JsonStringEnumMemberName("CAR_601_1200")] Car601To1200,
    [JsonStringEnumMemberName("CAR_1201_1800")] Car1201To1800,
    [JsonStringEnumMemberName("CAR_1801_2400")] Car1801To2400,
    [JsonStringEnumMemberName("CAR_2401_3000")] Car2401To3000,
    [JsonStringEnumMemberName("CAR_3001_4200")] Car3001To4200,
    [JsonStringEnumMemberName("CAR_4201_5400")] Car4201To5400,
    [JsonStringEnumMemberName("CAR_5401_6600")] Car5401To6600,
    [JsonStringEnumMemberName("CAR_6601_7800")] Car6601To7800,
    [JsonStringEnumMemberName("CAR_GT_7800")] CarGt7800,
    [JsonStringEnumMemberName("CAR_ELECTRIC")] CarElectric,
    [JsonStringEnumMemberName("MC_LE_50")] McLe50,
    [JsonStringEnumMemberName("MC_51_250")] Mc51To250,
    [JsonStringEnumMemberName("MC_251_550")] Mc251To550,
    [JsonStringEnumMemberName("MC_GT_550")] McGt550,
}
