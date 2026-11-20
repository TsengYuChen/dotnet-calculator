using System.Text.Json.Nodes;

namespace VehicleGo.IntegrationTests;

/// <summary>Request bodies (design §1.4). "Today" is 2026-11-19 (Asia/Taipei).</summary>
public static class Inputs
{
    /// <summary>A car about 7 years old (CUJ 4 data, no licence date).</summary>
    public static JsonObject Car(string plate) => new()
    {
        ["plate"] = plate,
        ["brand"] = "Toyota",
        ["model"] = "Altis",
        ["vehicleType"] = "CAR",
        ["displacementBracket"] = "CAR_1201_1800",
        ["manufactureDate"] = "2019-06-01",
        ["originalIssueDate"] = "2019-07-15",
    };

    /// <summary>CUJ 3: 出廠 103/05/01、原發照 103/08/20，排氣量 1201–1800cc，沒填指定檢驗日期。</summary>
    public static JsonObject Cuj3Car(string plate) => new()
    {
        ["plate"] = plate,
        ["brand"] = "Honda",
        ["model"] = "Civic",
        ["nickname"] = "老車",
        ["color"] = "白",
        ["vehicleType"] = "CAR",
        ["displacementBracket"] = "CAR_1201_1800",
        ["manufactureDate"] = "2014-05-01",
        ["originalIssueDate"] = "2014-08-20",
        ["licenceInspectionDate"] = null,
    };

    /// <summary>CUJ 5: 大型重機 551cc 以上，出廠 108/09/01、原發照 108/11/25。</summary>
    public static JsonObject Cuj5HeavyMc(string plate) => new()
    {
        ["plate"] = plate,
        ["brand"] = "Yamaha",
        ["model"] = "MT-09",
        ["vehicleType"] = "HEAVY_MC",
        ["displacementBracket"] = "MC_GT_550",
        ["manufactureDate"] = "2019-09-01",
        ["originalIssueDate"] = "2019-11-25",
    };

    /// <summary>CUJ 6: 電動機車，出廠 110/03/01、原發照 110/03/15。</summary>
    public static JsonObject Cuj6ElectricMc(string plate) => new()
    {
        ["plate"] = plate,
        ["brand"] = "Gogoro",
        ["model"] = "S2",
        ["vehicleType"] = "ELECTRIC_MC",
        ["manufactureDate"] = "2021-03-01",
        ["originalIssueDate"] = "2021-03-15",
    };

    public static JsonObject FuelMc(string plate) => new()
    {
        ["plate"] = plate,
        ["brand"] = "Kymco",
        ["model"] = "Many",
        ["vehicleType"] = "FUEL_MC",
        ["displacementBracket"] = "MC_51_250",
        ["manufactureDate"] = "2018-03-10",
        ["originalIssueDate"] = "2018-04-02",
    };

    public static JsonObject With(this JsonObject input, string field, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(input);
        var copy = (JsonObject)input.DeepClone();
        copy[field] = value;
        return copy;
    }

    public static JsonObject Without(this JsonObject input, string field)
    {
        ArgumentNullException.ThrowIfNull(input);
        var copy = (JsonObject)input.DeepClone();
        copy.Remove(field);
        return copy;
    }

    /// <summary>An UpdateVehicleRequest: the input plus <c>version</c>.</summary>
    public static JsonObject Update(JsonObject input, uint version) => input.With("version", version);
}
