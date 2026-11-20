using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Common;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class RestoreVehicleCommandHandlerTests
{
    private readonly AppTestHarness _h = new();

    private Task<Result> Restore(Guid id) =>
        new RestoreVehicleCommandHandler(_h.Repository, _h.Time).HandleAsync(new RestoreVehicleCommand(id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task 成功_清除刪除時間並儲存_不用交易也不檢查版本號()
    {
        // CUJ 7
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-29));

        var result = await Restore(v.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(v.DeletedAt);
        Assert.Equal(Now, v.UpdatedAt);
        Assert.Equal(["Get(includeTrashed=True)", "Save"], _h.Log.Calls);
        Assert.Null(_h.Repository.LastExpectedVersion);
        Assert.Equal(0, _h.UnitOfWork.Executions);
    }

    [Fact]
    public async Task 不存在_VEHICLE_NOT_FOUND() =>
        Assert.Equal(ErrorCode.VehicleNotFound, (await Restore(Guid.NewGuid())).Error!.Code);

    [Fact]
    public async Task 別人的車_VEHICLE_NOT_FOUND()
    {
        var other = _h.Seed(Details(), owner: OtherUserId, trashedAt: Now.AddDays(-1));

        Assert.Equal(ErrorCode.VehicleNotFound, (await Restore(other.Id)).Error!.Code);
        Assert.NotNull(other.DeletedAt);
    }

    [Fact]
    public async Task 垃圾桶滿30天_VEHICLE_NOT_FOUND()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-30));

        Assert.Equal(ErrorCode.VehicleNotFound, (await Restore(v.Id)).Error!.Code);
        Assert.NotNull(v.DeletedAt);
    }

    [Fact]
    public async Task 不在垃圾桶_VEHICLE_NOT_IN_TRASH()
    {
        var v = _h.Seed(Details());

        var result = await Restore(v.Id);

        Assert.Equal(ErrorCode.VehicleNotInTrash, result.Error!.Code);
        Assert.DoesNotContain("Save", _h.Log.Calls);
    }
}
