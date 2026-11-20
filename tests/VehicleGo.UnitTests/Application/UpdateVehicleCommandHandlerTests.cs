using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;
using VehicleGo.UnitTests.Fakes;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class UpdateVehicleCommandHandlerTests
{
    private readonly AppTestHarness _h = new();
    private readonly CapturingLogger<UpdateVehicleCommandHandler> _logger;

    public UpdateVehicleCommandHandlerTests() => _logger = _h.Logger<UpdateVehicleCommandHandler>();

    private UpdateVehicleCommandHandler Handler => new(_h.Repository, _h.UnitOfWork, _h.Rules, _h.Time, _logger);

    private Task<Result<VehicleDto>> Update(Guid id, uint version, VehicleDetails details) =>
        Handler.HandleAsync(new UpdateVehicleCommand(id, version, details), TestContext.Current.CancellationToken);

    [Fact]
    public async Task 成功_流程順序為鎖定_清除_取出_查車牌_儲存_提交()
    {
        var v = _h.Seed(Details(plate: "ABC-1234"));
        var version = v.Version;

        var result = await Update(v.Id, version, Details(plate: "ABC-1234", brand: "Honda"));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["BEGIN", "Lock", "Purge", "Get(includeTrashed=True)", "FindByPlate", $"Save(version={version})", "COMMIT"],
            _h.Log.Calls);
    }

    [Fact]
    public async Task 成功_整筆取代並回傳新的版本號和檢驗項目()
    {
        var v = _h.Seed(Details(plate: "ABC-1234"));
        var oldVersion = v.Version;

        // CUJ 4
        var result = await Update(v.Id, oldVersion, Details(plate: "XYZ-9999", nickname: "小白", licence: D(2027, 3, 10)));

        var dto = result.Value;
        Assert.Equal(v.Id, dto.Id);
        Assert.NotEqual(oldVersion, dto.Version);
        Assert.Equal(v.Version, dto.Version);
        Assert.Equal("XYZ-9999", dto.Plate);
        Assert.Equal("XYZ9999", v.NormalizedPlate);
        Assert.Equal("小白", dto.Nickname);
        Assert.Equal(D(2027, 3, 10), dto.LicenceInspectionDate);
        Assert.Equal(Now, v.UpdatedAt);
        Assert.Equal(new InspectionDto(InspectionKind.Periodic, InspectionStatus.Upcoming, D(2027, 2, 10), D(2027, 4, 10),
            "RTS44-CAR-LICENCE", false, InspectionBasis.Licence), Assert.Single(dto.Inspections));
    }

    [Fact]
    public async Task 驗證失敗_在交易之前回傳()
    {
        var v = _h.Seed(Details());

        var result = await Update(v.Id, v.Version, Details(type: VehicleType.ElectricMc));

        Assert.Equal(ErrorCode.BracketTypeMismatch, result.Error!.Code);
        Assert.Equal(0, _h.UnitOfWork.Executions);
        Assert.Empty(_h.Log.Calls);
    }

    [Fact]
    public async Task 驗證失敗優先於不存在()
    {
        var result = await Update(Guid.NewGuid(), 1, Details(manufacture: D(2027, 1, 1), issue: D(2027, 1, 1)));

        Assert.Equal(ErrorCode.DateInFuture, result.Error!.Code);
    }

    [Fact]
    public async Task 不存在_VEHICLE_NOT_FOUND()
    {
        var result = await Update(Guid.NewGuid(), 1, Details());

        Assert.Equal(ErrorCode.VehicleNotFound, result.Error!.Code);
        Assert.Equal(["BEGIN", "Lock", "Purge", "Get(includeTrashed=True)", "ROLLBACK"], _h.Log.Calls);
    }

    [Fact]
    public async Task 別人的車_VEHICLE_NOT_FOUND()
    {
        var other = _h.Seed(Details(), owner: OtherUserId);

        var result = await Update(other.Id, other.Version, Details());

        Assert.Equal(ErrorCode.VehicleNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task 在垃圾桶_VEHICLE_IN_TRASH()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-1));

        var result = await Update(v.Id, v.Version, Details());

        Assert.Equal(ErrorCode.VehicleInTrash, result.Error!.Code);
    }

    [Fact]
    public async Task 垃圾桶滿30天_被清除後VEHICLE_NOT_FOUND()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-31));

        var result = await Update(v.Id, v.Version, Details());

        Assert.Equal(ErrorCode.VehicleNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task 在垃圾桶且版本號不一致_先回VEHICLE_IN_TRASH()
    {
        var v = _h.Seed(Details(), trashedAt: Now.AddDays(-1));

        var result = await Update(v.Id, v.Version + 7, Details());

        Assert.Equal(ErrorCode.VehicleInTrash, result.Error!.Code);
    }

    [Fact]
    public async Task 版本號不一致_VEHICLE_MODIFIED且不查車牌()
    {
        // CUJ 9
        var v = _h.Seed(Details());

        var result = await Update(v.Id, v.Version - 1, Details(brand: "Honda"));

        Assert.Equal(ErrorCode.VehicleModified, result.Error!.Code);
        Assert.Equal(["BEGIN", "Lock", "Purge", "Get(includeTrashed=True)", "ROLLBACK"], _h.Log.Calls);
        Assert.Equal("Toyota", v.Brand);
    }

    [Fact]
    public async Task 舊版本號加重複車牌_回VEHICLE_MODIFIED()
    {
        _h.Seed(Details(plate: "DUP-0001"));
        var v = _h.Seed(Details(plate: "ABC-1234"));

        var result = await Update(v.Id, v.Version - 1, Details(plate: "DUP-0001"));

        Assert.Equal(ErrorCode.VehicleModified, result.Error!.Code);
    }

    [Fact]
    public async Task 車牌和使用中的其他車重複_PLATE_EXISTS_ACTIVE()
    {
        _h.Seed(Details(plate: "DUP-0001"));
        var v = _h.Seed(Details(plate: "ABC-1234"));

        var result = await Update(v.Id, v.Version, Details(plate: "dup 0001"));

        Assert.Equal(ErrorCode.PlateExistsActive, result.Error!.Code);
        Assert.Equal("ROLLBACK", _h.Log.Calls[^1]);
        Assert.Equal("ABC1234", v.NormalizedPlate);
    }

    [Fact]
    public async Task 車牌和垃圾桶裡的車重複_PLATE_EXISTS_IN_TRASH並附trashedVehicleId()
    {
        var trashed = _h.Seed(Details(plate: "DUP-0001"), trashedAt: Now.AddDays(-2));
        var v = _h.Seed(Details(plate: "ABC-1234"));

        var result = await Update(v.Id, v.Version, Details(plate: "DUP-0001"));

        Assert.Equal(ErrorCode.PlateExistsInTrash, result.Error!.Code);
        Assert.Equal(trashed.Id, result.Error.Extensions["trashedVehicleId"]);
    }

    [Fact]
    public async Task 車牌不變_不算和自己重複()
    {
        var v = _h.Seed(Details(plate: "ABC-1234"));

        var result = await Update(v.Id, v.Version, Details(plate: "abc 1234"));

        Assert.True(result.IsSuccess);
        Assert.Equal("abc 1234", result.Value.Plate);
    }

    [Fact]
    public async Task 改成垃圾桶滿30天的車的車牌_清除後成功()
    {
        _h.Seed(Details(plate: "OLD-0001"), trashedAt: Now.AddDays(-30));
        var v = _h.Seed(Details(plate: "ABC-1234"));

        var result = await Update(v.Id, v.Version, Details(plate: "OLD-0001"));

        Assert.True(result.IsSuccess);
        Assert.Single(_h.Store.Vehicles);
    }

    [Fact]
    public async Task 儲存時版本號衝突_後備的VEHICLE_MODIFIED()
    {
        var v = _h.Seed(Details());
        _h.Repository.SaveResult = Result.Failure(ErrorCode.VehicleModified);

        var result = await Update(v.Id, v.Version, Details(licence: D(2027, 3, 10)));

        Assert.Equal(ErrorCode.VehicleModified, result.Error!.Code);
        Assert.Equal("ROLLBACK", _h.Log.Calls[^1]);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task 指定檢驗日期不一致_提交後寫LICENCE_DATE_MISMATCH且不含車牌()
    {
        var v = _h.Seed(Details(plate: "LIC-0310"));

        var result = await Update(v.Id, v.Version, Details(plate: "LIC-0310", licence: D(2027, 3, 10)));

        Assert.True(result.IsSuccess);
        Assert.Equal("COMMIT", _h.Log.Calls[^2]);
        Assert.Equal("LOG:LICENCE_DATE_MISMATCH", _h.Log.Calls[^1]);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(v.Id, entry.Fields["VehicleId"]);
        Assert.Equal("2027-03-10", entry.Fields["LicenceInspectionDate"]);
        Assert.Equal("2027-07-15", entry.Fields["FormulaDate"]);
        CreateVehicleCommandHandlerTests.AssertNoPlate(entry, "LIC-0310", "LIC0310");
    }

    [Fact]
    public async Task 規則表缺規則_提交後寫INSPECTION_UNCOMPUTABLE()
    {
        _h.Rules.Rules = _h.Rules.Rules.Without(VehicleType.Car, InspectionKind.Periodic);
        var v = _h.Seed(Details());

        var result = await Update(v.Id, v.Version, Details());

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal("INSPECTION_UNCOMPUTABLE", entry.EventId.Name);
        Assert.Equal("PERIODIC", entry.Fields["Kind"]);
        Assert.Equal("NO_RULE", entry.Fields["Reason"]);
    }
}
