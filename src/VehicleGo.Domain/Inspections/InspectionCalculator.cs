using VehicleGo.Domain.Inspections.Rules;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Domain.Inspections;

/// <summary>
/// The rule engine (design §1.3): a pure function of vehicle data, today and the rule table.
/// Windows are calculated on read and never stored.
/// </summary>
public static class InspectionCalculator
{
    private const int SemiannualFromAgeYears = 10;
    private const int ExhaustFromAgeYears = 5;

    private static readonly IReadOnlyList<InspectionKind> PeriodicOnly = [InspectionKind.Periodic];
    private static readonly IReadOnlyList<InspectionKind> ExhaustOnly = [InspectionKind.Exhaust];
    private static readonly IReadOnlyList<InspectionKind> PeriodicAndExhaust = [InspectionKind.Periodic, InspectionKind.Exhaust];

    /// <summary>Inspection kinds that apply to each vehicle type, in display order.</summary>
    public static IReadOnlyList<InspectionKind> KindsFor(VehicleType vehicleType) => vehicleType switch
    {
        VehicleType.Car => PeriodicOnly,
        VehicleType.FuelMc => ExhaustOnly,
        VehicleType.HeavyMc => PeriodicAndExhaust,
        VehicleType.ElectricMc => ExhaustOnly,
        _ => [],
    };

    public static InspectionResult Calculate(Vehicle vehicle, DateOnly today, RuleTable rules)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        return Calculate(vehicle.ToInspectionInput(), today, rules);
    }

    public static InspectionResult Calculate(InspectionInput input, DateOnly today, RuleTable rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);

        var kinds = KindsFor(input.VehicleType);
        var items = new List<InspectionItem>(kinds.Count);
        var uncomputable = new List<UncomputableDiagnostic>();

        if (IsInvalidInput(input))
        {
            foreach (var kind in kinds)
            {
                items.Add(Uncomputable(kind));
                uncomputable.Add(new UncomputableDiagnostic(kind, UncomputableReason.InvalidInput));
            }

            return new InspectionResult(items, new InspectionDiagnostics(uncomputable, false, null));
        }

        var mismatch = false;
        DateOnly? formulaDate = null;

        foreach (var kind in kinds)
        {
            var rule = rules.Find(input.VehicleType, kind);
            if (rule is null)
            {
                items.Add(Uncomputable(kind));
                uncomputable.Add(new UncomputableDiagnostic(kind, UncomputableReason.NoRule));
                continue;
            }

            if (rule.Calculation == PeriodCalculation.Exempt)
            {
                items.Add(new InspectionItem(kind, InspectionStatus.Exempt, null, null,
                    rule.RuleIdFor(VehicleAge.FullYears(input.ManufactureDate, today)), InspectionBasis.Formula, false));
                continue;
            }

            var useLicence = rule.Calculation == PeriodCalculation.AnnualByAnniversary
                && input.LicenceInspectionDate is not null
                && rule.LicenceRuleId is not null;
            var basis = useLicence ? InspectionBasis.Licence : InspectionBasis.Formula;

            var windows = rule.Calculation == PeriodCalculation.AnnualByAnniversary
                ? PeriodicWindows(input, useLicence)
                : ExhaustWindows(input);
            var window = windows.First(w => w.Deadline >= today);

            var status = window.Earliest <= today && today <= window.Deadline
                ? InspectionStatus.InWindow
                : InspectionStatus.Upcoming;
            var ruleId = useLicence
                ? rule.LicenceRuleId!
                : rule.RuleIdFor(VehicleAge.FullYears(input.ManufactureDate, today));

            items.Add(new InspectionItem(kind, status, window.Earliest, window.Deadline, ruleId, basis, window.Estimated));

            if (useLicence)
            {
                (mismatch, formulaDate) = CheckLicenceDate(input, input.LicenceInspectionDate!.Value);
            }
        }

        return new InspectionResult(items, new InspectionDiagnostics(uncomputable, mismatch, formulaDate));
    }

    /// <summary>
    /// Only "cannot be computed without this" conditions (design §1.3 InvalidInput). Today-dependent
    /// validation (future dates, LICENCE_DATE_TOO_FAR) is not checked: stored data is shown if computable.
    /// </summary>
    private static bool IsInvalidInput(InspectionInput input) =>
        input.OriginalIssueDate < input.ManufactureDate
        || (input.LicenceInspectionDate is not null && !DisplacementBrackets.LicenceDateAllowed(input.VehicleType))
        || !DisplacementBrackets.IsValidFor(input.VehicleType, input.DisplacementBracket);

    private static InspectionItem Uncomputable(InspectionKind kind) =>
        new(kind, InspectionStatus.Uncomputable, null, null, null, InspectionBasis.Formula, false);

    private readonly record struct Window(DateOnly Earliest, DateOnly Deadline, bool Estimated);

    /// <summary>
    /// 定期檢驗 windows in deadline order. Anchors are L + n years (n = -1, 0, 1, ...; n = -1 only yields
    /// its estimated window) or issue + (GapYears + n) years (n = 0, 1, ...), each added directly to the
    /// start date. On an anchor where the vehicle is 10+ years old, an estimated window at anchor + 6 months follows.
    /// </summary>
    private static IEnumerable<Window> PeriodicWindows(InspectionInput input, bool useLicence)
    {
        DateOnly start;
        int firstN;
        if (useLicence)
        {
            start = input.LicenceInspectionDate!.Value;
            firstN = -1;
        }
        else
        {
            start = input.OriginalIssueDate;
            firstN = FirstInspectionDate.GapYears(input.ManufactureDate, input.OriginalIssueDate);
        }

        var baseOffset = useLicence ? 0 : firstN;
        for (var years = firstN; ; years++)
        {
            var anchor = start.AddYears(years);
            if (years >= baseOffset)
            {
                yield return AroundByMonth(anchor, estimated: false);
            }

            if (VehicleAge.FullYears(input.ManufactureDate, anchor) >= SemiannualFromAgeYears)
            {
                yield return AroundByMonth(anchor.AddMonths(6), estimated: true);
            }
        }
    }

    private static Window AroundByMonth(DateOnly date, bool estimated) =>
        new(date.AddMonths(-1), date.AddMonths(1), estimated);

    /// <summary>
    /// 排氣定檢 windows: for each year Y from the manufacture year, the 1st of the month before the issue
    /// month to the last day of the month after; only windows whose deadline is on or after manufacture + 5 years.
    /// </summary>
    private static IEnumerable<Window> ExhaustWindows(InspectionInput input)
    {
        var month = input.OriginalIssueDate.Month;
        var requiredFrom = input.ManufactureDate.AddYears(ExhaustFromAgeYears);
        for (var year = input.ManufactureDate.Year; ; year++)
        {
            var first = new DateOnly(year, month, 1);
            var after = first.AddMonths(1);
            var window = new Window(
                first.AddMonths(-1),
                new DateOnly(after.Year, after.Month, DateTime.DaysInMonth(after.Year, after.Month)),
                false);
            if (window.Deadline >= requiredFrom)
            {
                yield return window;
            }
        }
    }

    /// <summary>
    /// Technical Plan §3 「不一致的判斷」: the licence date is consistent when its month/day equals the
    /// formula date of the same year, or (when that formula anchor's vehicle age is 10+) the month/day of a
    /// formula date + 6 months in the same year. A licence date before the first formula date is compared
    /// with the first formula date's month/day.
    /// </summary>
    private static (bool Mismatch, DateOnly FormulaDate) CheckLicenceDate(InspectionInput input, DateOnly licence)
    {
        var gap = FirstInspectionDate.GapYears(input.ManufactureDate, input.OriginalIssueDate);
        var first = input.OriginalIssueDate.AddYears(gap);

        if (licence < first)
        {
            return (!SameMonthDay(licence, first), first);
        }

        var sameYear = input.OriginalIssueDate.AddYears(licence.Year - input.OriginalIssueDate.Year);
        if (SameMonthDay(licence, sameYear))
        {
            return (false, sameYear);
        }

        // Formula anchors whose + 6 months falls in the licence date's year: anchors in that year or the year before.
        foreach (var anchorYear in new[] { licence.Year - 1, licence.Year })
        {
            var anchor = input.OriginalIssueDate.AddYears(anchorYear - input.OriginalIssueDate.Year);
            if (anchor < first || VehicleAge.FullYears(input.ManufactureDate, anchor) < SemiannualFromAgeYears)
            {
                continue;
            }

            var estimated = anchor.AddMonths(6);
            if (estimated.Year == licence.Year && SameMonthDay(licence, estimated))
            {
                return (false, sameYear);
            }
        }

        return (true, sameYear);
    }

    private static bool SameMonthDay(DateOnly a, DateOnly b) => a.Month == b.Month && a.Day == b.Day;
}
