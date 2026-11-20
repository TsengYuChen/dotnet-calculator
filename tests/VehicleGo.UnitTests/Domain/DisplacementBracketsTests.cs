using VehicleGo.Domain.Inspections.Rules;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Domain;

/// <summary>Technical Plan §2「車種 → 可用級距 → 必填」對照表與 D-R4 級距（使用牌照稅法，規則版本日期 2026-01-01）。</summary>
public class DisplacementBracketsTests
{
    private static readonly DisplacementBracket[] CarBrackets =
    [
        DisplacementBracket.CarLe500, DisplacementBracket.Car501To600, DisplacementBracket.Car601To1200,
        DisplacementBracket.Car1201To1800, DisplacementBracket.Car1801To2400, DisplacementBracket.Car2401To3000,
        DisplacementBracket.Car3001To4200, DisplacementBracket.Car4201To5400, DisplacementBracket.Car5401To6600,
        DisplacementBracket.Car6601To7800, DisplacementBracket.CarGt7800, DisplacementBracket.CarElectric,
    ];

    [Fact]
    public void 級距清單包含每個代碼一次_順序同enum()
    {
        Assert.Equal(Enum.GetValues<DisplacementBracket>(), DisplacementBrackets.All.Select(b => b.Code));
    }

    [Fact]
    public void 每個級距只屬於一個車種()
    {
        Assert.All(DisplacementBrackets.All, b => Assert.Single(b.VehicleTypes));
    }

    [Fact]
    public void 級距中文標籤()
    {
        var labels = DisplacementBrackets.All.ToDictionary(b => b.Code, b => b.Label);
        Assert.Equal("500cc 以下", labels[DisplacementBracket.CarLe500]);
        Assert.Equal("501–600cc", labels[DisplacementBracket.Car501To600]);
        Assert.Equal("601–1200cc", labels[DisplacementBracket.Car601To1200]);
        Assert.Equal("1201–1800cc", labels[DisplacementBracket.Car1201To1800]);
        Assert.Equal("1801–2400cc", labels[DisplacementBracket.Car1801To2400]);
        Assert.Equal("2401–3000cc", labels[DisplacementBracket.Car2401To3000]);
        Assert.Equal("3001–4200cc", labels[DisplacementBracket.Car3001To4200]);
        Assert.Equal("4201–5400cc", labels[DisplacementBracket.Car4201To5400]);
        Assert.Equal("5401–6600cc", labels[DisplacementBracket.Car5401To6600]);
        Assert.Equal("6601–7800cc", labels[DisplacementBracket.Car6601To7800]);
        Assert.Equal("7801cc 以上", labels[DisplacementBracket.CarGt7800]);
        Assert.Equal("電動", labels[DisplacementBracket.CarElectric]);
        Assert.Equal("50cc 以下", labels[DisplacementBracket.McLe50]);
        Assert.Equal("51–250cc", labels[DisplacementBracket.Mc51To250]);
        Assert.Equal("251–550cc", labels[DisplacementBracket.Mc251To550]);
        Assert.Equal("551cc 以上", labels[DisplacementBracket.McGt550]);
    }

    [Fact]
    public void 汽車級距附使用牌照稅法出處()
    {
        Assert.All(DisplacementBrackets.All.Where(b => b.VehicleTypes.Contains(VehicleType.Car)),
            b => Assert.Equal(LegalCitation.LicenseTaxAct, b.Citation));
    }

    [Fact]
    public void 車種對照表()
    {
        var car = DisplacementBrackets.For(VehicleType.Car);
        Assert.Equal("自用小客車（非營業用）", car.Label);
        Assert.Equal(CarBrackets, car.Brackets);
        Assert.True(car.BracketRequired);
        Assert.True(car.LicenceDateAllowed);

        var fuel = DisplacementBrackets.For(VehicleType.FuelMc);
        Assert.Equal("一般燃油機車", fuel.Label);
        Assert.Equal([DisplacementBracket.McLe50, DisplacementBracket.Mc51To250], fuel.Brackets);
        Assert.True(fuel.BracketRequired);
        Assert.False(fuel.LicenceDateAllowed);

        var heavy = DisplacementBrackets.For(VehicleType.HeavyMc);
        Assert.Equal("大型重型機車", heavy.Label);
        Assert.Equal([DisplacementBracket.Mc251To550, DisplacementBracket.McGt550], heavy.Brackets);
        Assert.True(heavy.BracketRequired);
        Assert.True(heavy.LicenceDateAllowed);

        var electric = DisplacementBrackets.For(VehicleType.ElectricMc);
        Assert.Equal("電動機車（40 馬力以下）", electric.Label);
        Assert.Empty(electric.Brackets);
        Assert.False(electric.BracketRequired);
        Assert.False(electric.LicenceDateAllowed);

        Assert.Equal(Enum.GetValues<VehicleType>(), DisplacementBrackets.VehicleTypes.Select(t => t.Type));
    }

    [Fact]
    public void 級距清單的車種和對照表一致()
    {
        foreach (var info in DisplacementBrackets.VehicleTypes)
        {
            Assert.Equal(info.Brackets,
                DisplacementBrackets.All.Where(b => b.VehicleTypes.Contains(info.Type)).Select(b => b.Code));
        }
    }

    [Fact]
    public void IsValidFor_每個車種和級距的組合()
    {
        foreach (var type in Enum.GetValues<VehicleType>())
        {
            var allowed = DisplacementBrackets.For(type).Brackets;
            Assert.Equal(type == VehicleType.ElectricMc, DisplacementBrackets.IsValidFor(type, null));
            foreach (var bracket in Enum.GetValues<DisplacementBracket>())
            {
                Assert.Equal(allowed.Contains(bracket), DisplacementBrackets.IsValidFor(type, bracket));
            }
        }

        Assert.False(DisplacementBrackets.IsValidFor((VehicleType)99, null));
        Assert.False(DisplacementBrackets.IsValidFor(VehicleType.Car, (DisplacementBracket)99));
    }

    [Fact]
    public void LicenceDateAllowed_只限汽車和大型重機()
    {
        Assert.True(DisplacementBrackets.LicenceDateAllowed(VehicleType.Car));
        Assert.True(DisplacementBrackets.LicenceDateAllowed(VehicleType.HeavyMc));
        Assert.False(DisplacementBrackets.LicenceDateAllowed(VehicleType.FuelMc));
        Assert.False(DisplacementBrackets.LicenceDateAllowed(VehicleType.ElectricMc));
    }
}
