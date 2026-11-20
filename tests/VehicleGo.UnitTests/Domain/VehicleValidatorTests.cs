using VehicleGo.Domain.Common;
using VehicleGo.Domain.Vehicles;
using static VehicleGo.UnitTests.Domain.TestData;

namespace VehicleGo.UnitTests.Domain;

/// <summary>Design §1.5: Domain validation codes and their order (first failure only).</summary>
public class VehicleValidatorTests
{
    private static void AssertError(Result result, ErrorCode code, string field)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(field, result.Error.Field);
    }

    [Fact]
    public void 合法資料通過()
    {
        Assert.True(VehicleValidator.Validate(Details(), Today).IsSuccess);
        Assert.True(VehicleValidator.Validate(Details(licence: D(2027, 3, 10)), Today).IsSuccess);
        Assert.True(VehicleValidator.Validate(
            Details(type: VehicleType.ElectricMc, bracket: null, manufacture: D(2021, 3, 1), issue: D(2021, 3, 15)), Today).IsSuccess);
        Assert.True(VehicleValidator.Validate(
            Details(type: VehicleType.HeavyMc, bracket: DisplacementBracket.McGt550, licence: D(2027, 3, 10)), Today).IsSuccess);
        Assert.True(VehicleValidator.Validate(
            Details(type: VehicleType.FuelMc, bracket: DisplacementBracket.McLe50), Today).IsSuccess);
    }

    [Theory]
    [InlineData("---")]
    [InlineData("   ")]
    [InlineData("")]
    public void 正規化後車牌為空_VALIDATION_FAILED(string plate)
    {
        AssertError(VehicleValidator.Validate(Details(plate: plate), Today), ErrorCode.ValidationFailed, "plate");
    }

    [Fact]
    public void 車牌超過16字_VALIDATION_FAILED()
    {
        Assert.True(VehicleValidator.Validate(Details(plate: new string('A', 16)), Today).IsSuccess);
        AssertError(VehicleValidator.Validate(Details(plate: new string('A', 17)), Today), ErrorCode.ValidationFailed, "plate");
    }

    [Fact]
    public void 廠牌型號必填且最多50字_暱稱顏色最多50字()
    {
        AssertError(VehicleValidator.Validate(Details(brand: " "), Today), ErrorCode.ValidationFailed, "brand");
        AssertError(VehicleValidator.Validate(Details(model: ""), Today), ErrorCode.ValidationFailed, "model");
        AssertError(VehicleValidator.Validate(Details(brand: new string('x', 51)), Today), ErrorCode.ValidationFailed, "brand");
        AssertError(VehicleValidator.Validate(Details(nickname: new string('x', 51)), Today), ErrorCode.ValidationFailed, "nickname");
        AssertError(VehicleValidator.Validate(Details(color: new string('x', 51)), Today), ErrorCode.ValidationFailed, "color");
        Assert.True(VehicleValidator.Validate(
            Details(brand: new string('x', 50), model: new string('x', 50), nickname: new string('x', 50), color: new string('x', 50)),
            Today).IsSuccess);
    }

    [Fact]
    public void 未定義的車種_VALIDATION_FAILED()
    {
        AssertError(VehicleValidator.Validate(Details(type: (VehicleType)99), Today), ErrorCode.ValidationFailed, "vehicleType");
    }

    [Fact]
    public void 出廠日期是未來日期_DATE_IN_FUTURE()
    {
        AssertError(VehicleValidator.Validate(Details(manufacture: D(2026, 11, 20), issue: D(2026, 11, 20)), Today),
            ErrorCode.DateInFuture, "manufactureDate");
    }

    [Fact]
    public void 原發照日期是未來日期_DATE_IN_FUTURE()
    {
        AssertError(VehicleValidator.Validate(Details(manufacture: D(2026, 1, 1), issue: D(2026, 11, 20)), Today),
            ErrorCode.DateInFuture, "originalIssueDate");
    }

    [Fact]
    public void 今天當天不算未來日期()
    {
        Assert.True(VehicleValidator.Validate(Details(manufacture: Today, issue: Today), Today).IsSuccess);
    }

    [Fact]
    public void 原發照日期早於出廠日期_ISSUE_BEFORE_MANUFACTURE()
    {
        AssertError(VehicleValidator.Validate(Details(manufacture: D(2019, 7, 15), issue: D(2019, 7, 14)), Today),
            ErrorCode.IssueBeforeManufacture, "originalIssueDate");
        Assert.True(VehicleValidator.Validate(Details(manufacture: D(2019, 7, 15), issue: D(2019, 7, 15)), Today).IsSuccess);
    }

    [Theory]
    [InlineData(VehicleType.Car, null)]
    [InlineData(VehicleType.Car, DisplacementBracket.Mc51To250)]
    [InlineData(VehicleType.FuelMc, null)]
    [InlineData(VehicleType.FuelMc, DisplacementBracket.Mc251To550)]
    [InlineData(VehicleType.HeavyMc, DisplacementBracket.Mc51To250)]
    [InlineData(VehicleType.HeavyMc, DisplacementBracket.CarGt7800)]
    [InlineData(VehicleType.ElectricMc, DisplacementBracket.McLe50)]
    [InlineData(VehicleType.ElectricMc, DisplacementBracket.CarElectric)]
    public void 級距和車種不符_BRACKET_TYPE_MISMATCH(VehicleType type, DisplacementBracket? bracket)
    {
        AssertError(VehicleValidator.Validate(Details(type: type, bracket: bracket), Today),
            ErrorCode.BracketTypeMismatch, "displacementBracket");
    }

    [Theory]
    [InlineData(VehicleType.FuelMc, DisplacementBracket.Mc51To250)]
    [InlineData(VehicleType.ElectricMc, null)]
    public void 燃油機車和電動機車不能填指定檢驗日期_LICENCE_DATE_NOT_ALLOWED(VehicleType type, DisplacementBracket? bracket)
    {
        AssertError(VehicleValidator.Validate(Details(type: type, bracket: bracket, licence: D(2027, 3, 10)), Today),
            ErrorCode.LicenceDateNotAllowed, "licenceInspectionDate");
    }

    [Fact]
    public void 指定檢驗日期早於原發照日期_LICENCE_DATE_BEFORE_ISSUE()
    {
        AssertError(VehicleValidator.Validate(Details(issue: D(2019, 7, 15), licence: D(2019, 7, 14)), Today),
            ErrorCode.LicenceDateBeforeIssue, "licenceInspectionDate");
        Assert.True(VehicleValidator.Validate(Details(issue: D(2019, 7, 15), licence: D(2019, 7, 15)), Today).IsSuccess);
    }

    [Fact]
    public void 指定檢驗日期晚於今年加2年的12月31日_LICENCE_DATE_TOO_FAR()
    {
        Assert.True(VehicleValidator.Validate(Details(licence: D(2028, 12, 31)), Today).IsSuccess);
        AssertError(VehicleValidator.Validate(Details(licence: D(2029, 1, 1)), Today),
            ErrorCode.LicenceDateTooFar, "licenceInspectionDate");
    }

    [Fact]
    public void 檢查順序_未來日期先於原發照早於出廠()
    {
        var details = Details(manufacture: D(2026, 12, 1), issue: D(2026, 11, 1));
        Assert.Equal(ErrorCode.DateInFuture, VehicleValidator.Validate(details, Today).Error!.Code);
    }

    [Fact]
    public void 檢查順序_原發照早於出廠先於級距()
    {
        var details = Details(bracket: null, manufacture: D(2019, 7, 15), issue: D(2019, 7, 1));
        Assert.Equal(ErrorCode.IssueBeforeManufacture, VehicleValidator.Validate(details, Today).Error!.Code);
    }

    [Fact]
    public void 檢查順序_級距先於指定檢驗日期()
    {
        var details = Details(type: VehicleType.FuelMc, bracket: null, licence: D(2027, 3, 10));
        Assert.Equal(ErrorCode.BracketTypeMismatch, VehicleValidator.Validate(details, Today).Error!.Code);
    }

    [Fact]
    public void 檢查順序_不允許先於早於原發照()
    {
        var details = Details(type: VehicleType.FuelMc, bracket: DisplacementBracket.McLe50, licence: D(2010, 1, 1));
        Assert.Equal(ErrorCode.LicenceDateNotAllowed, VehicleValidator.Validate(details, Today).Error!.Code);
    }

    [Fact]
    public void 檢查順序_VALIDATION_FAILED最先()
    {
        var details = Details(plate: "--", manufacture: D(2027, 1, 1), issue: D(2026, 1, 1));
        Assert.Equal(ErrorCode.ValidationFailed, VehicleValidator.Validate(details, Today).Error!.Code);
    }
}
