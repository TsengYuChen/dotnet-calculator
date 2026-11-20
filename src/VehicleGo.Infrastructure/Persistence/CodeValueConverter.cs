using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>
/// Code enum ↔ its fixed code string (design §1.1), e.g. <c>HEAVY_MC</c>. Applied to every code enum in
/// <see cref="AppDbContext.ConfigureConventions"/>. Not <c>HasConversion&lt;string&gt;()</c>, which stores
/// the member name (<c>HeavyMc</c>). Unknown codes and undefined values throw.
/// </summary>
public sealed class CodeValueConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => CodeEnum<TEnum>.ToCode(value),
    code => CodeEnum<TEnum>.FromCode(code))
    where TEnum : struct, Enum;
