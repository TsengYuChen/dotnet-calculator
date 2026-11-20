using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class ListTrashQueryHandlerTests
{
    private readonly AppTestHarness _h = new();

    private async Task<IReadOnlyList<TrashItemDto>> List() =>
        (await new ListTrashQueryHandler(_h.Queries, _h.Time).HandleAsync(new ListTrashQuery(), TestContext.Current.CancellationToken)).Value;

    [Fact]
    public async Task 回傳垃圾桶裡的車_purgeAt是刪除時間加30天()
    {
        var deletedAt = Now.AddDays(-3).AddHours(-5);
        var v = _h.Seed(Details(plate: "TRS-0001", type: VehicleType.ElectricMc, bracket: null, brand: "Gogoro", model: "S2", nickname: "小藍"),
            trashedAt: deletedAt);
        _h.Seed(Details(plate: "ACT-0001"));

        var item = Assert.Single(await List());

        Assert.Equal(new TrashItemDto(v.Id, "TRS-0001", "Gogoro", "S2", "小藍", VehicleType.ElectricMc, deletedAt, deletedAt.AddDays(30)), item);
    }

    [Fact]
    public async Task 用現在減30天當截止時間_滿30天的車看不到()
    {
        _h.Seed(Details(plate: "OLD-0001"), trashedAt: Now.AddDays(-30));
        _h.Seed(Details(plate: "NEW-0001"), trashedAt: Now.AddDays(-30).AddSeconds(1));

        var list = await List();

        Assert.Equal(Now.AddDays(-30), _h.Queries.LastCutoff);
        Assert.Equal(["NEW-0001"], list.Select(t => t.Plate));
    }

    [Fact]
    public async Task 最近刪除的在前()
    {
        _h.Seed(Details(plate: "AAA-0001"), trashedAt: Now.AddDays(-5));
        _h.Seed(Details(plate: "BBB-0001"), trashedAt: Now.AddDays(-1));
        _h.Seed(Details(plate: "CCC-0001"), trashedAt: Now.AddDays(-3));

        Assert.Equal(["BBB-0001", "CCC-0001", "AAA-0001"], (await List()).Select(t => t.Plate));
    }

    [Fact]
    public async Task 不含別人的垃圾桶()
    {
        _h.Seed(Details(plate: "OTH-0001"), owner: OtherUserId, trashedAt: Now.AddDays(-1));

        Assert.Empty(await List());
    }
}
