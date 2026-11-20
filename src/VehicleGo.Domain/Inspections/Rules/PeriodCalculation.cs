namespace VehicleGo.Domain.Inspections.Rules;

/// <summary>How a rule computes its inspection windows (design §1.3).</summary>
public enum PeriodCalculation
{
    /// <summary>
    /// 定期檢驗: yearly window of ±1 month (by day) around the 指定檢驗日期 — the entered licence
    /// date L (basis LICENCE) or the formula date 原發照日期 + GapYears (basis FORMULA); once the
    /// vehicle is 10 years old on an anchor date, an extra estimated window at anchor + 6 months.
    /// </summary>
    AnnualByAnniversary,

    /// <summary>排氣定檢: yearly window from the 1st of the month before the issue month to the last day of the month after.</summary>
    AnnualByIssueMonth,

    /// <summary>免檢.</summary>
    Exempt,
}
