using System.Reflection;
using System.Text.Json.Serialization;

namespace VehicleGo.Application.Common;

/// <summary>The fixed code of a code enum member (design §1.1), e.g. <c>HEAVY_MC</c>, for log fields.</summary>
internal static class Codes
{
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        return typeof(TEnum).GetField(name)?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? name;
    }
}
