using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Inspections.Rules;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

public class VehicleTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VehicleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 建立時驗證並正規化車牌()
    {
        var result = Vehicle.Create(VehicleId, Owner, Details(plate: "ａｂｃ-1234", nickname: "小白", color: "白"), Today, Now);

        Assert.True(result.IsSuccess);
        var v = result.Value;
        Assert.Equal(VehicleId, v.Id);
        Assert.Equal(Owner, v.OwnerId);
        Assert.Equal("ａｂｃ-1234", v.Plate);
        Assert.Equal("ABC1234", v.NormalizedPlate);
        Assert.Equal("Toyota", v.Brand);
        Assert.Equal("Altis", v.Model);
        Assert.Equal("小白", v.Nickname);
        Assert.Equal("白", v.Color);
        Assert.Equal(VehicleType.Car, v.VehicleType);
        Assert.Equal(DisplacementBracket.Car1201To1800, v.DisplacementBracket);
        Assert.Equal(D(2019, 6, 1), v.ManufactureDate);
        Assert.Equal(D(2019, 7, 15), v.OriginalIssueDate);
        Assert.Null(v.LicenceInspectionDate);
        Assert.Null(v.DeletedAt);
        Assert.False(v.IsTrashed);
        Assert.Equal(Now, v.CreatedAt);
        Assert.Equal(Now, v.UpdatedAt);
    }

    [Fact]
    public void 建立時驗證失敗回傳錯誤代碼()
    {
        var result = Vehicle.Create(VehicleId, Owner, Details(manufacture: D(2027, 1, 1), issue: D(2027, 1, 1)), Today, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.DateInFuture, result.Error.Code);
    }

    [Fact]
    public void 修改時整筆取代_驗證失敗時不改變()
    {
        var v = Vehicle.Create(VehicleId, Owner, Details(), Today, Now).Value;
        var later = Now.AddHours(1);

        var failed = v.Update(Details(plate: "NEW-1", type: VehicleType.ElectricMc), Today, later);
        Assert.Equal(ErrorCode.BracketTypeMismatch, failed.Error!.Code);
        Assert.Equal("ABC1234", v.NormalizedPlate);
        Assert.Equal(Now, v.UpdatedAt);

        var ok = v.Update(Details(plate: "new-1", type: VehicleType.ElectricMc, bracket: null), Today, later);
        Assert.True(ok.IsSuccess);
        Assert.Equal("NEW1", v.NormalizedPlate);
        Assert.Equal(VehicleType.ElectricMc, v.VehicleType);
        Assert.Null(v.DisplacementBracket);
        Assert.Equal(Now, v.CreatedAt);
        Assert.Equal(later, v.UpdatedAt);
    }

    [Fact]
    public void 丟進垃圾桶和還原()
    {
        var v = Vehicle.Create(VehicleId, Owner, Details(), Today, Now).Value;
        var trashedAt = Now.AddDays(1);

        Assert.True(v.Trash(trashedAt).IsSuccess);
        Assert.True(v.IsTrashed);
        Assert.Equal(trashedAt, v.DeletedAt);
        Assert.Equal(ErrorCode.VehicleInTrash, v.Trash(trashedAt.AddDays(1)).Error!.Code);
        Assert.Equal(trashedAt, v.DeletedAt);

        var restoredAt = Now.AddDays(2);
        Assert.True(v.Restore(restoredAt).IsSuccess);
        Assert.False(v.IsTrashed);
        Assert.Null(v.DeletedAt);
        Assert.Equal(restoredAt, v.UpdatedAt);
        Assert.Equal(ErrorCode.VehicleNotInTrash, v.Restore(restoredAt).Error!.Code);
    }

    [Fact]
    public void 已在垃圾桶的車不能修改()
    {
        var v = Vehicle.Create(VehicleId, Owner, Details(), Today, Now).Value;
        v.Trash(Now);

        Assert.Equal(ErrorCode.VehicleInTrash, v.Update(Details(), Today, Now).Error!.Code);
    }

    [Fact]
    public void RTS44_20260101_實體可以直接交給規則引擎()
    {
        var v = Vehicle.Create(VehicleId, Owner,
            Details(manufacture: D(2014, 5, 1), issue: D(2014, 8, 20)), Today, Now).Value;

        var item = Assert.Single(InspectionCalculator.Calculate(v, Today, RuleTable.Current()).Items);
        Assert.Equal(D(2027, 1, 20), item.EarliestDate);
        Assert.True(item.Estimated);
    }

    [Fact]
    public void 車輛上限為50台_垃圾桶30天()
    {
        Assert.Equal(50, VehicleLimits.MaxVehiclesPerOwner);
        Assert.Equal(30, VehicleLimits.TrashRetentionDays);
    }
}
