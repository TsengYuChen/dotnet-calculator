using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json.Serialization;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>
/// The fixed codes of a code enum (design §1.1), read from <see cref="JsonStringEnumMemberNameAttribute"/>:
/// the single code table shared with the JSON converter.
/// </summary>
public static class CodeEnum<TEnum> where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> CodesByValue = typeof(TEnum)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToFrozenDictionary(
            f => (TEnum)f.GetValue(null)!,
            f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                 ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{f.Name} has no [JsonStringEnumMemberName] code."));

    private static readonly FrozenDictionary<string, TEnum> ValuesByCode =
        CodesByValue.ToFrozenDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    /// <summary>All codes, in declaration order.</summary>
    public static IReadOnlyList<string> Codes { get; } = Enum.GetValues<TEnum>().Select(v => CodesByValue[v]).ToArray();

    public static string ToCode(TEnum value) =>
        CodesByValue.TryGetValue(value, out var code)
            ? code
            : throw new InvalidOperationException($"{value} is not a defined {typeof(TEnum).Name} value.");

    public static TEnum FromCode(string code) =>
        ValuesByCode.TryGetValue(code, out var value)
            ? value
            : throw new InvalidOperationException($"'{code}' is not a {typeof(TEnum).Name} code.");

    /// <summary>SQL list of quoted codes, e.g. <c>'CAR', 'FUEL_MC'</c> (codes are fixed upper-case identifiers).</summary>
    public static string SqlList(IEnumerable<TEnum> values) => string.Join(", ", values.Select(v => $"'{ToCode(v)}'"));
}

/// <summary>True for enums whose members carry <see cref="JsonStringEnumMemberNameAttribute"/> (code enums).</summary>
internal static class CodeEnums
{
    public static bool IsCodeEnum(Type type) =>
        type.IsEnum && type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Any(f => f.IsDefined(typeof(JsonStringEnumMemberNameAttribute)));
}
