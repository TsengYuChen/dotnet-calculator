using VehicleGo.Domain.Inspections.Rules;

namespace VehicleGo.Domain.Vehicles;

/// <summary>A displacement bracket with its label, applicable vehicle types and legal source.</summary>
/// <param name="Citation">使用牌照稅法 for car brackets; null for motorcycle brackets (recorded only, Overview).</param>
public sealed record BracketInfo(
    DisplacementBracket Code,
    string Label,
    IReadOnlyList<VehicleType> VehicleTypes,
    LegalCitation? Citation);

/// <summary>Per-type rules of Technical Plan §2 「車種 → 可用級距 → 必填」.</summary>
public sealed record VehicleTypeInfo(
    VehicleType Type,
    string Label,
    IReadOnlyList<DisplacementBracket> Brackets,
    bool BracketRequired,
    bool LicenceDateAllowed);

/// <summary>Fixed bracket list (D-R4, design §1.1) and the vehicle type → bracket table (Technical Plan §2).</summary>
public static class DisplacementBrackets
{
    private static BracketInfo Car(DisplacementBracket code, string label) =>
        new(code, label, [VehicleType.Car], LegalCitation.LicenseTaxAct);

    private static BracketInfo Motorcycle(DisplacementBracket code, string label, VehicleType type) =>
        new(code, label, [type], null);

    public static IReadOnlyList<BracketInfo> All { get; } =
    [
        Car(DisplacementBracket.CarLe500, "500cc 以下"),
        Car(DisplacementBracket.Car501To600, "501–600cc"),
        Car(DisplacementBracket.Car601To1200, "601–1200cc"),
        Car(DisplacementBracket.Car1201To1800, "1201–1800cc"),
        Car(DisplacementBracket.Car1801To2400, "1801–2400cc"),
        Car(DisplacementBracket.Car2401To3000, "2401–3000cc"),
        Car(DisplacementBracket.Car3001To4200, "3001–4200cc"),
        Car(DisplacementBracket.Car4201To5400, "4201–5400cc"),
        Car(DisplacementBracket.Car5401To6600, "5401–6600cc"),
        Car(DisplacementBracket.Car6601To7800, "6601–7800cc"),
        Car(DisplacementBracket.CarGt7800, "7801cc 以上"),
        Car(DisplacementBracket.CarElectric, "電動"),
        Motorcycle(DisplacementBracket.McLe50, "50cc 以下", VehicleType.FuelMc),
        Motorcycle(DisplacementBracket.Mc51To250, "51–250cc", VehicleType.FuelMc),
        Motorcycle(DisplacementBracket.Mc251To550, "251–550cc", VehicleType.HeavyMc),
        Motorcycle(DisplacementBracket.McGt550, "551cc 以上", VehicleType.HeavyMc),
    ];

    private static IReadOnlyList<DisplacementBracket> BracketsOf(VehicleType type) =>
        All.Where(b => b.VehicleTypes.Contains(type)).Select(b => b.Code).ToArray();

    public static IReadOnlyList<VehicleTypeInfo> VehicleTypes { get; } =
    [
        new(VehicleType.Car, "自用小客車（非營業用）", BracketsOf(VehicleType.Car), BracketRequired: true, LicenceDateAllowed: true),
        new(VehicleType.FuelMc, "一般燃油機車", BracketsOf(VehicleType.FuelMc), BracketRequired: true, LicenceDateAllowed: false),
        new(VehicleType.HeavyMc, "大型重型機車", BracketsOf(VehicleType.HeavyMc), BracketRequired: true, LicenceDateAllowed: true),
        new(VehicleType.ElectricMc, "電動機車（40 馬力以下）", [], BracketRequired: false, LicenceDateAllowed: false),
    ];

    public static VehicleTypeInfo For(VehicleType vehicleType) =>
        VehicleTypes.FirstOrDefault(t => t.Type == vehicleType)
        ?? throw new ArgumentOutOfRangeException(nameof(vehicleType), vehicleType, "Unknown vehicle type.");

    /// <summary>
    /// True when <paramref name="bracket"/> is allowed for the type: a bracket of that type when the
    /// bracket is required, otherwise (ELECTRIC_MC) only null. Unknown types or brackets are never valid.
    /// </summary>
    public static bool IsValidFor(VehicleType vehicleType, DisplacementBracket? bracket)
    {
        var info = VehicleTypes.FirstOrDefault(t => t.Type == vehicleType);
        if (info is null)
        {
            return false;
        }

        return bracket is null
            ? !info.BracketRequired
            : info.Brackets.Contains(bracket.Value);
    }

    public static bool LicenceDateAllowed(VehicleType vehicleType) =>
        VehicleTypes.FirstOrDefault(t => t.Type == vehicleType)?.LicenceDateAllowed ?? false;
}
