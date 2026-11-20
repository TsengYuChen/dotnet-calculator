namespace VehicleGo.Domain.Inspections.Rules;

public static class VehicleAge
{
    /// <summary>
    /// Full years from <paramref name="manufactureDate"/> to <paramref name="date"/>: the largest N with
    /// <c>date &gt;= manufactureDate.AddYears(N)</c> (Technical Plan §3 「滿 N 年」). A 2/29 manufacture
    /// date reaches its anniversary on 2/28 in non-leap years.
    /// </summary>
    public static int FullYears(DateOnly manufactureDate, DateOnly date)
    {
        var years = date.Year - manufactureDate.Year;
        if (date < manufactureDate.AddYears(years))
        {
            years--;
        }

        return years;
    }
}
