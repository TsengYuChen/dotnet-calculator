using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Common;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class TrashVehicleCommandHandlerTests
{
    private readonly AppTestHarness _h = new();

    private Task<Result> Trash(Guid id) =>
        new TrashVehicleCommandHandler(_h.Repository, _h.Time).HandleAsync(new TrashVehicleCommand(id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task 成功_設定刪除時間並儲存_不用交易也不檢查版本號()
    {
        var v = _h.Seed(Details());

        var result = await Trash(v.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, v.DeletedAt);
        Assert.Equal(["Get(includeTrashed=True)", "Save"], _h.Log.Calls);
        Assert.Null(_h.Repository.LastExpectedVersion);
        Assert.Equal(0, _h.UnitOfWork.Executions);
        Assert.Equal(Now.AddDays(-30), _h.Repository.LastCutoff);
    }

    [Fact]
    public async Task 不存在_VEHICLE_NOT_FOUND() =>
        Assert.Equal(ErrorCode.VehicleNotFound, (await Trash(Guid.NewGuid())).Error!.Code);

    [Fact]
    public async Task 別人的車_VEHICLE_NOT_FOUND()
    {
        var other = _h.Seed(Details(), owner: OtherUserId);

        Assert.Equal(ErrorCode.VehicleNotFound, (await Trash(other.Id)).Error!.Code);
        Assert.Null(other.DeletedAt);
    }

    [Fact]
    public async Task 已在垃圾桶_VEHICLE_IN_TRASH()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-1));

        var result = await Trash(v.Id);

        Assert.Equal(ErrorCode.VehicleInTrash, result.Error!.Code);
        Assert.Equal(Now.AddDays(-1), v.DeletedAt);
        Assert.DoesNotContain("Save", _h.Log.Calls);
    }

    [Fact]
    public async Task 已在垃圾桶滿30天_VEHICLE_NOT_FOUND()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-30));

        Assert.Equal(ErrorCode.VehicleNotFound, (await Trash(v.Id)).Error!.Code);
    }
}
