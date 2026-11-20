using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class GetVehicleQueryHandlerTests
{
    private readonly AppTestHarness _h = new();

    private Task<Result<VehicleDto>> Get(Guid id) =>
        new GetVehicleQueryHandler(_h.Queries, _h.Rules, _h.Time).HandleAsync(new GetVehicleQuery(id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task 回傳所有欄位_檢驗項目和版本號()
    {
        // CUJ 5
        var v = _h.Seed(Details(plate: "HMC-0001", type: VehicleType.HeavyMc, bracket: DisplacementBracket.McGt550,
            manufacture: D(2019, 9, 1), issue: D(2019, 11, 25), brand: "Yamaha", model: "MT-09", nickname: "藍", color: "藍"));

        var dto = (await Get(v.Id)).Value;

        Assert.Equal(
            new VehicleDto(v.Id, v.Version, "HMC-0001", "Yamaha", "MT-09", "藍", "藍", VehicleType.HeavyMc, DisplacementBracket.McGt550,
                D(2019, 9, 1), D(2019, 11, 25), null, dto.Inspections),
            dto);
        Assert.Equal(
            [
                new InspectionDto(InspectionKind.Periodic, InspectionStatus.InWindow, D(2026, 10, 25), D(2026, 12, 25), "RTS44-HMC-AGE5TO10", false, InspectionBasis.Formula),
                new InspectionDto(InspectionKind.Exhaust, InspectionStatus.InWindow, D(2026, 10, 1), D(2026, 12, 31), "APC40-MC-ANNUAL", false, InspectionBasis.Formula),
            ],
            dto.Inspections);
    }

    [Fact]
    public async Task 免檢的欄位值()
    {
        // CUJ 6
        var v = _h.Seed(Details(type: VehicleType.ElectricMc, bracket: null, manufacture: D(2021, 3, 1), issue: D(2021, 3, 15)));

        var inspection = Assert.Single((await Get(v.Id)).Value.Inspections);

        Assert.Equal(new InspectionDto(InspectionKind.Exhaust, InspectionStatus.Exempt, null, null, "APC40-EMC-EXEMPT", false, InspectionBasis.Formula), inspection);
    }

    [Fact]
    public async Task 不存在_VEHICLE_NOT_FOUND() =>
        Assert.Equal(ErrorCode.VehicleNotFound, (await Get(Guid.NewGuid())).Error!.Code);

    [Fact]
    public async Task 垃圾桶裡的車_VEHICLE_NOT_FOUND()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-1));

        Assert.Equal(ErrorCode.VehicleNotFound, (await Get(v.Id)).Error!.Code);
    }

    [Fact]
    public async Task 別人的車_VEHICLE_NOT_FOUND()
    {
        var v = _h.Seed(Details(), owner: OtherUserId);

        Assert.Equal(ErrorCode.VehicleNotFound, (await Get(v.Id)).Error!.Code);
    }
}
