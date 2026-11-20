using System.Text.Json.Serialization;

namespace VehicleGo.Domain.Vehicles;

/// <summary>Vehicle type codes (design §1.1). Labels and per-type rules: <see cref="DisplacementBrackets"/>.</summary>
public enum VehicleType
{
    /// <summary>自用小客車（非營業用）</summary>
    [JsonStringEnumMemberName("CAR")] Car,

    /// <summary>一般燃油機車</summary>
    [JsonStringEnumMemberName("FUEL_MC")] FuelMc,

    /// <summary>大型重型機車</summary>
    [JsonStringEnumMemberName("HEAVY_MC")] HeavyMc,

    /// <summary>電動機車（40 馬力以下）</summary>
    [JsonStringEnumMemberName("ELECTRIC_MC")] ElectricMc,
}
