namespace VehicleGo.Domain.Inspections.Rules;

/// <summary>D-R2: 第一次指定檢驗日期 = 原發照日期 + GapYears (Technical Plan §3 間隔年數表).</summary>
public static class FirstInspectionDate
{
    /// <summary>
    /// Years to add to the issue date: smallest N in 1..4 with <c>issue &lt;= manufacture.AddYears(N)</c>
    /// (「未逾 N 年」 includes exactly N years) gives 6 − N; more than 4 years gives 1.
    /// </summary>
    public static int GapYears(DateOnly manufactureDate, DateOnly originalIssueDate)
    {
        for (var n = 1; n <= 4; n++)
        {
            if (originalIssueDate <= manufactureDate.AddYears(n))
            {
                return 6 - n;
            }
        }

        return 1;
    }

    public static DateOnly For(DateOnly manufactureDate, DateOnly originalIssueDate) =>
        originalIssueDate.AddYears(GapYears(manufactureDate, originalIssueDate));
}
