using Microsoft.Extensions.Logging;
using VehicleGo.Application.Vehicles;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Vehicles;
using VehicleGo.UnitTests.Fakes;
using static VehicleGo.UnitTests.Application.AppTestHarness;

namespace VehicleGo.UnitTests.Application;

public class CreateVehicleCommandHandlerTests
{
    private readonly AppTestHarness _h = new();
    private readonly CapturingLogger<CreateVehicleCommandHandler> _logger;

    public CreateVehicleCommandHandlerTests() => _logger = _h.Logger<CreateVehicleCommandHandler>();

    private CreateVehicleCommandHandler Handler =>
        new(_h.Repository, _h.UnitOfWork, _h.CurrentUser, _h.Rules, _h.Time, _logger);

    private Task<Result<VehicleDto>> Create(VehicleDetails details) =>
        Handler.HandleAsync(new CreateVehicleCommand(details), TestContext.Current.CancellationToken);

    [Fact]
    public async Task 成功_流程順序為鎖定_清除_查車牌_計數_新增_提交()
    {
        var result = await Create(Details());

        Assert.True(result.IsSuccess);
        Assert.Equal(["BEGIN", "Lock", "Purge", "FindByPlate", "Count", "Add", "COMMIT"], _h.Log.Calls);
    }

    [Fact]
    public async Task 成功_回傳含檢驗項目和版本號的車輛並以目前使用者為擁有者()
    {
        // CUJ 3
        var result = await Create(Details(plate: "abc-1234", manufacture: D(2014, 5, 1), issue: D(2014, 8, 20)));

        var dto = result.Value;
        var stored = Assert.Single(_h.Store.Vehicles);
        Assert.Equal(stored.Id, dto.Id);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(FakeCurrentUser.DefaultUserId, stored.OwnerId);
        Assert.Equal(stored.Version, dto.Version);
        Assert.NotEqual(0u, dto.Version);
        Assert.Equal("abc-1234", dto.Plate);
        Assert.Equal("ABC1234", stored.NormalizedPlate);
        Assert.Equal(Now, stored.CreatedAt);
        var inspection = Assert.Single(dto.Inspections);
        Assert.Equal(new InspectionDto(InspectionKind.Periodic, InspectionStatus.Upcoming, D(2027, 1, 20), D(2027, 3, 20),
            "RTS44-CAR-AGE10UP", true, InspectionBasis.Formula), inspection);
    }

    [Fact]
    public async Task 驗證失敗_在交易之前回傳且不碰Repository()
    {
        var result = await Create(Details(manufacture: D(2027, 1, 1), issue: D(2027, 1, 2)));

        Assert.Equal(ErrorCode.DateInFuture, result.Error!.Code);
        Assert.Equal("manufactureDate", result.Error.Field);
        Assert.Equal(0, _h.UnitOfWork.Executions);
        Assert.Empty(_h.Log.Calls);
    }

    [Fact]
    public async Task 今天用台北時間判斷未來日期()
    {
        // 2026-11-19T16:30Z is already 2026-11-20 in Taipei.
        _h.Time.SetUtcNow(new DateTimeOffset(2026, 11, 19, 16, 30, 0, TimeSpan.Zero));

        var result = await Create(Details(manufacture: D(2026, 11, 20), issue: D(2026, 11, 20)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task 清除截止時間是現在減30天()
    {
        await Create(Details());

        Assert.Equal(Now.AddDays(-30), _h.Repository.LastCutoff);
    }

    [Fact]
    public async Task 車牌和使用中的車重複_PLATE_EXISTS_ACTIVE並回滾()
    {
        _h.Seed(Details(plate: "ABC-1234"));

        var result = await Create(Details(plate: "ａｂｃ 1234"));

        Assert.Equal(ErrorCode.PlateExistsActive, result.Error!.Code);
        Assert.Equal(["BEGIN", "Lock", "Purge", "FindByPlate", "ROLLBACK"], _h.Log.Calls);
        Assert.Single(_h.Store.Vehicles);
    }

    [Fact]
    public async Task 車牌和垃圾桶裡的車重複_PLATE_EXISTS_IN_TRASH並附trashedVehicleId()
    {
        // CUJ 7
        var trashed = _h.Seed(Details(plate: "ABC-1234"), trashedAt: Now.AddDays(-3));

        var result = await Create(Details(plate: "abc1234"));

        Assert.Equal(ErrorCode.PlateExistsInTrash, result.Error!.Code);
        Assert.Equal(trashed.Id, result.Error.Extensions["trashedVehicleId"]);
        Assert.Equal("ROLLBACK", _h.Log.Calls[^1]);
    }

    [Fact]
    public async Task 垃圾桶裡滿30天的車先被清除_可以用同一個車牌新增()
    {
        _h.Seed(Details(plate: "ABC-1234"), trashedAt: Now.AddDays(-30));

        var result = await Create(Details(plate: "ABC-1234"));

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.Id, Assert.Single(_h.Store.Vehicles).Id);
    }

    [Fact]
    public async Task 達到50台_VEHICLE_LIMIT_REACHED並附使用中和垃圾桶數量()
    {
        // CUJ 8
        _h.SeedMany(active: 47, trashed: 3);

        var result = await Create(Details(plate: "NEW-0001"));

        Assert.Equal(ErrorCode.VehicleLimitReached, result.Error!.Code);
        Assert.Equal(47, result.Error.Extensions["activeCount"]);
        Assert.Equal(3, result.Error.Extensions["trashCount"]);
        Assert.Equal(["BEGIN", "Lock", "Purge", "FindByPlate", "Count", "ROLLBACK"], _h.Log.Calls);
        Assert.Equal(50, _h.Store.Vehicles.Count);
    }

    [Fact]
    public async Task 有49台時可以新增第50台()
    {
        _h.SeedMany(active: 46, trashed: 3);

        var result = await Create(Details(plate: "NEW-0001"));

        Assert.True(result.IsSuccess);
        Assert.Equal(50, _h.Store.Vehicles.Count);
    }

    [Fact]
    public async Task 車輛數不含別人的車()
    {
        for (var i = 0; i < 50; i++)
        {
            _h.Seed(Details(plate: $"OTH-{i:D4}"), owner: OtherUserId);
        }

        var result = await Create(Details(plate: "OTH-0001"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task 同時寫入時撞到唯一索引_回傳Repository的錯誤並回滾()
    {
        _h.Repository.AddResult = Result.Failure(ErrorCode.PlateExistsActive);

        var result = await Create(Details());

        Assert.Equal(ErrorCode.PlateExistsActive, result.Error!.Code);
        Assert.Equal("ROLLBACK", _h.Log.Calls[^1]);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task 指定檢驗日期不一致_提交後寫LICENCE_DATE_MISMATCH且不含車牌()
    {
        // CUJ 4: formula date is 7/15 every year; the licence date 3/10 disagrees.
        var result = await Create(Details(plate: "LIC-0310", manufacture: D(2019, 6, 1), issue: D(2019, 7, 15), licence: D(2027, 3, 10)));

        Assert.True(result.IsSuccess);
        Assert.Equal(["BEGIN", "Lock", "Purge", "FindByPlate", "Count", "Add", "COMMIT", "LOG:LICENCE_DATE_MISMATCH"], _h.Log.Calls);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("LICENCE_DATE_MISMATCH", entry.EventId.Name);
        Assert.Equal(result.Value.Id, entry.Fields["VehicleId"]);
        Assert.Equal("CAR", entry.Fields["VehicleType"]);
        Assert.Equal("2019-06-01", entry.Fields["ManufactureDate"]);
        Assert.Equal("2019-07-15", entry.Fields["OriginalIssueDate"]);
        Assert.Equal("2027-03-10", entry.Fields["LicenceInspectionDate"]);
        Assert.Equal("2027-07-15", entry.Fields["FormulaDate"]);
        AssertNoPlate(entry, "LIC-0310", "LIC0310");
    }

    [Fact]
    public async Task 指定檢驗日期一致_不寫log()
    {
        // CUJ 4c: 8/20 equals the formula month/day.
        var result = await Create(Details(manufacture: D(2014, 5, 1), issue: D(2014, 8, 20), licence: D(2027, 8, 20)));

        Assert.True(result.IsSuccess);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task 規則表缺規則_提交後寫INSPECTION_UNCOMPUTABLE()
    {
        _h.Rules.Rules = _h.Rules.Rules.Without(VehicleType.HeavyMc, InspectionKind.Exhaust);

        var result = await Create(Details(plate: "HMC-0001", type: VehicleType.HeavyMc, bracket: DisplacementBracket.McGt550,
            manufacture: D(2019, 9, 1), issue: D(2019, 11, 25)));

        Assert.True(result.IsSuccess);
        Assert.Equal("COMMIT", _h.Log.Calls[^2]);
        Assert.Equal("LOG:INSPECTION_UNCOMPUTABLE", _h.Log.Calls[^1]);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal("INSPECTION_UNCOMPUTABLE", entry.EventId.Name);
        Assert.Equal(result.Value.Id, entry.Fields["VehicleId"]);
        Assert.Equal("EXHAUST", entry.Fields["Kind"]);
        Assert.Equal("NO_RULE", entry.Fields["Reason"]);
        AssertNoPlate(entry, "HMC-0001", "HMC0001");
        Assert.Equal(InspectionStatus.Uncomputable, Assert.Single(result.Value.Inspections, i => i.Kind == InspectionKind.Exhaust).Status);
        Assert.Equal(InspectionStatus.InWindow, Assert.Single(result.Value.Inspections, i => i.Kind == InspectionKind.Periodic).Status);
    }

    [Fact]
    public async Task 失敗時不寫log()
    {
        _h.Seed(Details(plate: "ABC-1234"));

        await Create(Details(plate: "ABC-1234", licence: D(2027, 3, 10)));

        Assert.Empty(_logger.Entries);
    }

    internal static void AssertNoPlate(LogEntry entry, params string[] plates)
    {
        foreach (var plate in plates)
        {
            Assert.DoesNotContain(plate, entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(entry.Fields.Values, v => v?.ToString()?.Contains(plate, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}
