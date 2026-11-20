namespace VehicleGo.Domain.Inspections.Rules;

/// <summary>Where a rule or code list comes from (design §1.2 法規出處).</summary>
/// <param name="Abbreviation">Prefix used in rule IDs and test names, e.g. <c>RTS44</c>.</param>
/// <param name="Law">法規名稱, e.g. 道路交通安全規則.</param>
/// <param name="Article">條號／範圍, e.g. 第 44 條.</param>
public sealed record LegalCitation(string Abbreviation, string Law, string Article)
{
    /// <summary>道路交通安全規則第 44 條.</summary>
    public static LegalCitation RoadTrafficSafetyRules44 { get; } = new("RTS44", "道路交通安全規則", "第 44 條");

    /// <summary>空氣污染防制法第 40 條.</summary>
    public static LegalCitation AirPollutionControlAct40 { get; } = new("APC40", "空氣污染防制法", "第 40 條");

    /// <summary>使用牌照稅法（汽車排氣量級距）.</summary>
    public static LegalCitation LicenseTaxAct { get; } = new("ULTA", "使用牌照稅法", "汽車排氣量級距");
}
