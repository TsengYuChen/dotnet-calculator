using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Common;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class DeleteVehicleCommandHandlerTests
{
    private readonly AppTestHarness _h = new();

    private Task<Result> Delete(Guid id) =>
        new DeleteVehicleCommandHandler(_h.Repository, _h.Time).HandleAsync(new DeleteVehicleCommand(id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task 成功_永久刪除垃圾桶裡的車_不用交易()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-1));

        var result = await Delete(v.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_h.Store.Vehicles);
        Assert.Equal(["Get(includeTrashed=True)", "Delete"], _h.Log.Calls);
        Assert.Equal(0, _h.UnitOfWork.Executions);
    }

    [Fact]
    public async Task 不存在_VEHICLE_NOT_FOUND() =>
        Assert.Equal(ErrorCode.VehicleNotFound, (await Delete(Guid.NewGuid())).Error!.Code);

    [Fact]
    public async Task 別人的車_VEHICLE_NOT_FOUND()
    {
        _h.Seed(Details(), owner: OtherUserId, trashedAt: Now.AddDays(-1));
        var other = _h.Store.Vehicles[0];

        Assert.Equal(ErrorCode.VehicleNotFound, (await Delete(other.Id)).Error!.Code);
        Assert.Single(_h.Store.Vehicles);
    }

    [Fact]
    public async Task 垃圾桶滿30天_VEHICLE_NOT_FOUND()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-31));

        Assert.Equal(ErrorCode.VehicleNotFound, (await Delete(v.Id)).Error!.Code);
    }

    [Fact]
    public async Task 不在垃圾桶_VEHICLE_NOT_IN_TRASH()
    {
        var v = _h.Seed(Details());

        var result = await Delete(v.Id);

        Assert.Equal(ErrorCode.VehicleNotInTrash, result.Error!.Code);
        Assert.Single(_h.Store.Vehicles);
    }
}
