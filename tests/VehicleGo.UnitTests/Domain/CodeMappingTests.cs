using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Users;
using VehicleGo.Domain.Vehicles;
using VehicleGo.Infrastructure.Persistence;

namespace VehicleGo.UnitTests.Domain;

/// <summary>
/// Design §1.1: every code enum member maps to its fixed code through <see cref="JsonStringEnumMemberNameAttribute"/>.
/// Both the JSON converter and the shared database <see cref="CodeValueConverter{TEnum}"/> use it.
/// </summary>
public class CodeMappingTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    /// <summary>Expected codes per enum, in declaration order (design §1.1 and §1.5).</summary>
    private static readonly Dictionary<Type, string[]> ExpectedCodes = new()
    {
        [typeof(VehicleType)] = ["CAR", "FUEL_MC", "HEAVY_MC", "ELECTRIC_MC"],
        [typeof(DisplacementBracket)] =
        [
            "CAR_LE_500", "CAR_501_600", "CAR_601_1200", "CAR_1201_1800", "CAR_1801_2400", "CAR_2401_3000",
            "CAR_3001_4200", "CAR_4201_5400", "CAR_5401_6600", "CAR_6601_7800", "CAR_GT_7800", "CAR_ELECTRIC",
            "MC_LE_50", "MC_51_250", "MC_251_550", "MC_GT_550",
        ],
        [typeof(InspectionKind)] = ["PERIODIC", "EXHAUST"],
        [typeof(InspectionStatus)] = ["EXEMPT", "UPCOMING", "IN_WINDOW", "UNCOMPUTABLE"],
        [typeof(InspectionBasis)] = ["FORMULA", "LICENCE"],
        [typeof(CalendarPreference)] = ["ROC", "GREGORIAN"],
        [typeof(UncomputableReason)] = ["NO_RULE", "INVALID_INPUT"],
        [typeof(ErrorCode)] =
        [
            "VALIDATION_FAILED", "ISSUE_BEFORE_MANUFACTURE", "DATE_IN_FUTURE", "BRACKET_TYPE_MISMATCH",
            "LICENCE_DATE_NOT_ALLOWED", "LICENCE_DATE_BEFORE_ISSUE", "LICENCE_DATE_TOO_FAR", "CSRF_INVALID",
            "VEHICLE_NOT_FOUND", "PLATE_EXISTS_ACTIVE", "PLATE_EXISTS_IN_TRASH", "VEHICLE_LIMIT_REACHED",
            "VEHICLE_MODIFIED", "VEHICLE_IN_TRASH", "VEHICLE_NOT_IN_TRASH",
        ],
    };

    public static TheoryData<Type> CodeEnums
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in ExpectedCodes.Keys)
            {
                data.Add(type);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CodeEnums))]
    public void 每個成員都有代碼attribute_且等於設計的代碼(Type enumType)
    {
        var codes = enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name)
            .ToArray();

        Assert.Equal(ExpectedCodes[enumType], codes);
    }

    [Theory]
    [MemberData(nameof(CodeEnums))]
    public void JSON來回轉換(Type enumType)
    {
        var values = Enum.GetValues(enumType);
        for (var i = 0; i < values.Length; i++)
        {
            var value = values.GetValue(i)!;
            var json = JsonSerializer.Serialize(value, enumType, Json);
            Assert.Equal($"\"{ExpectedCodes[enumType][i]}\"", json);
            Assert.Equal(value, JsonSerializer.Deserialize(json, enumType, Json));
        }
    }

    [Theory]
    [MemberData(nameof(CodeEnums))]
    public void JSON不接受數字和enum名稱(Type enumType)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("0", enumType, Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("\"0\"", enumType, Json));

        var memberName = Enum.GetNames(enumType)[0];
        if (!ExpectedCodes[enumType].Contains(memberName))
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize($"\"{memberName}\"", enumType, Json));
        }
    }

    private static ValueConverter DbConverter(Type enumType) =>
        (ValueConverter)Activator.CreateInstance(typeof(CodeValueConverter<>).MakeGenericType(enumType))!;

    [Theory]
    [MemberData(nameof(CodeEnums))]
    public void 資料庫來回轉換(Type enumType)
    {
        var converter = DbConverter(enumType);
        var values = Enum.GetValues(enumType);

        Assert.Equal(typeof(string), converter.ProviderClrType);
        for (var i = 0; i < values.Length; i++)
        {
            var value = values.GetValue(i)!;
            Assert.Equal(ExpectedCodes[enumType][i], converter.ConvertToProvider(value));
            Assert.Equal(value, converter.ConvertFromProvider(ExpectedCodes[enumType][i]));
        }
    }

    [Theory]
    [MemberData(nameof(CodeEnums))]
    public void 資料庫不接受enum名稱和數字(Type enumType)
    {
        var converter = DbConverter(enumType);

        Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider("0"));
        Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider("UNKNOWN_CODE"));
        var memberName = Enum.GetNames(enumType)[0];
        if (!ExpectedCodes[enumType].Contains(memberName))
        {
            Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider(memberName));
        }
    }

    [Fact]
    public void 資料庫不寫入未定義的enum值() =>
        Assert.Throws<InvalidOperationException>(() => DbConverter(typeof(VehicleType)).ConvertToProvider((VehicleType)99));

    [Fact]
    public void JSON不認得的代碼解析失敗()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<VehicleType>("\"TRUCK\"", Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DisplacementBracket>("\"CAR_LE500\"", Json));
    }

    [Fact]
    public void 所有代碼enum都在測試清單中()
    {
        var domainEnums = typeof(VehicleType).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.IsPublic && t.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Any(f => f.IsDefined(typeof(JsonStringEnumMemberNameAttribute))))
            .ToHashSet();

        Assert.Equal(domainEnums, ExpectedCodes.Keys.ToHashSet());
    }
}
