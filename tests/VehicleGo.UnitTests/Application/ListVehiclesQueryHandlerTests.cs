using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class ListVehiclesQueryHandlerTests
{
    private readonly AppTestHarness _h = new();

    private async Task<IReadOnlyList<VehicleDto>> List(string? sort)
    {
        var result = await new ListVehiclesQueryHandler(_h.Queries, _h.Rules, _h.Time)
            .HandleAsync(new ListVehiclesQuery(sort), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    // Deadlines on 2026-11-19 (see the CUJs):
    private void SeedCujVehicles()
    {
        // CUJ 12: 2028-05-10, manufactured 2023-03-01
        _h.Seed(Details(plate: "CUJ-0012", manufacture: D(2023, 3, 1), issue: D(2023, 4, 10)));
        // CUJ 6: EXEMPT, manufactured 2021-03-01
        _h.Seed(Details(plate: "CUJ-0006", type: VehicleType.ElectricMc, bracket: null, manufacture: D(2021, 3, 1), issue: D(2021, 3, 15)));
        // CUJ 3: 2027-03-20, manufactured 2014-05-01
        _h.Seed(Details(plate: "CUJ-0003", manufacture: D(2014, 5, 1), issue: D(2014, 8, 20)));
        // CUJ 5: periodic 2026-12-25, exhaust 2026-12-31, manufactured 2019-09-01
        _h.Seed(Details(plate: "CUJ-0005", type: VehicleType.HeavyMc, bracket: DisplacementBracket.McGt550,
            manufacture: D(2019, 9, 1), issue: D(2019, 11, 25)));
    }

    [Fact]
    public async Task 預設依最早的最後期限排序_免檢排最後()
    {
        SeedCujVehicles();

        var list = await List(null);

        Assert.Equal(["CUJ-0005", "CUJ-0003", "CUJ-0012", "CUJ-0006"], list.Select(v => v.Plate));
    }

    [Fact]
    public async Task 依最後期限排序和預設相同()
    {
        SeedCujVehicles();

        Assert.Equal((await List(null)).Select(v => v.Id), (await List("deadline")).Select(v => v.Id));
    }

    [Fact]
    public async Task 依最後期限排序_用該車所有檢驗中最早的最後期限()
    {
        // Heavy MC: periodic deadline 2026-12-25, exhaust 2026-12-31. Car CUJ 4c-like deadline 2026-12-28 sits between.
        _h.Seed(Details(plate: "HMC-0001", type: VehicleType.HeavyMc, bracket: DisplacementBracket.McGt550,
            manufacture: D(2019, 9, 1), issue: D(2019, 11, 25)));
        _h.Seed(Details(plate: "AAA-0001", manufacture: D(2019, 9, 1), issue: D(2019, 11, 28)));

        var list = await List("deadline");

        Assert.Equal(["HMC-0001", "AAA-0001"], list.Select(v => v.Plate));
    }

    [Fact]
    public async Task 依最後期限排序_同日依車牌()
    {
        _h.Seed(Details(plate: "CCC-0001", manufacture: D(2014, 5, 1), issue: D(2014, 8, 20)));
        _h.Seed(Details(plate: "AAA-0001", manufacture: D(2014, 5, 1), issue: D(2014, 8, 20)));
        _h.Seed(Details(plate: "BBB-0001", manufacture: D(2014, 5, 1), issue: D(2014, 8, 20)));

        var list = await List("deadline");

        Assert.Equal(["AAA-0001", "BBB-0001", "CCC-0001"], list.Select(v => v.Plate));
    }

    [Fact]
    public async Task 依最後期限排序_無法計算和免檢排最後並依車牌()
    {
        _h.Rules.Rules = _h.Rules.Rules.Without(VehicleType.FuelMc, InspectionKind.Exhaust);
        _h.Seed(Details(plate: "ZZZ-0001", type: VehicleType.ElectricMc, bracket: null, manufacture: D(2021, 3, 1), issue: D(2021, 3, 15)));
        _h.Seed(Details(plate: "MMM-0001", type: VehicleType.FuelMc, bracket: DisplacementBracket.Mc51To250,
            manufacture: D(2018, 3, 1), issue: D(2018, 3, 10)));
        _h.Seed(Details(plate: "YYY-0001", manufacture: D(2023, 3, 1), issue: D(2023, 4, 10)));
        _h.Seed(Details(plate: "AAA-0001", type: VehicleType.ElectricMc, bracket: null, manufacture: D(2022, 3, 1), issue: D(2022, 3, 15)));

        var list = await List("deadline");

        Assert.Equal(["YYY-0001", "AAA-0001", "MMM-0001", "ZZZ-0001"], list.Select(v => v.Plate));
        Assert.Equal(InspectionStatus.Uncomputable, Assert.Single(list[2].Inspections).Status);
    }

    [Fact]
    public async Task 依最後期限排序_大型重機一種檢驗無法計算時用另一種的期限()
    {
        _h.Rules.Rules = _h.Rules.Rules.Without(VehicleType.HeavyMc, InspectionKind.Exhaust);
        _h.Seed(Details(plate: "AAA-0001", type: VehicleType.ElectricMc, bracket: null, manufacture: D(2021, 3, 1), issue: D(2021, 3, 15)));
        _h.Seed(Details(plate: "ZZZ-0001", type: VehicleType.HeavyMc, bracket: DisplacementBracket.McGt550,
            manufacture: D(2019, 9, 1), issue: D(2019, 11, 25)));

        var list = await List("deadline");

        Assert.Equal(["ZZZ-0001", "AAA-0001"], list.Select(v => v.Plate));
    }

    [Fact]
    public async Task 依出廠日期排序_舊的在前()
    {
        SeedCujVehicles();

        var list = await List("manufactureDate");

        Assert.Equal(["CUJ-0003", "CUJ-0005", "CUJ-0006", "CUJ-0012"], list.Select(v => v.Plate));
    }

    [Fact]
    public async Task 依出廠日期排序_同日依車牌()
    {
        _h.Seed(Details(plate: "CCC-0001", manufacture: D(2020, 1, 1), issue: D(2020, 2, 1)));
        _h.Seed(Details(plate: "AAA-0001", manufacture: D(2020, 1, 1), issue: D(2020, 5, 1)));
        _h.Seed(Details(plate: "BBB-0001", manufacture: D(2019, 1, 1), issue: D(2019, 2, 1)));

        var list = await List("manufactureDate");

        Assert.Equal(["BBB-0001", "AAA-0001", "CCC-0001"], list.Select(v => v.Plate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Deadline")]
    [InlineData("manufacture_date")]
    [InlineData("0")]
    public async Task 不合法的sort_VALIDATION_FAILED(string sort)
    {
        var result = await new ListVehiclesQueryHandler(_h.Queries, _h.Rules, _h.Time)
            .HandleAsync(new ListVehiclesQuery(sort), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Equal("sort", result.Error.Field);
    }

    [Fact]
    public async Task 只含目前使用者使用中的車_含檢驗項目和版本號()
    {
        var mine = _h.Seed(Details(plate: "CUJ-0003", manufacture: D(2014, 5, 1), issue: D(2014, 8, 20)));
        _h.Seed(Details(plate: "TRS-0001"), trashedAt: Now.AddDays(-1));
        _h.Seed(Details(plate: "OTH-0001"), owner: OtherUserId);

        var dto = Assert.Single(await List(null));

        Assert.Equal(mine.Id, dto.Id);
        Assert.Equal(mine.Version, dto.Version);
        Assert.Equal(new InspectionDto(InspectionKind.Periodic, InspectionStatus.Upcoming, D(2027, 1, 20), D(2027, 3, 20),
            "RTS44-CAR-AGE10UP", true, InspectionBasis.Formula), Assert.Single(dto.Inspections));
    }

    [Fact]
    public async Task 沒有車時回傳空清單() => Assert.Empty(await List(null));
}
