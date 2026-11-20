using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Inspections.Rules;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Domain;

/// <summary>Shared helpers. Rule tests use a fixed "today" of 2026-11-19 (民國 115/11/19).</summary>
internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 11, 19);

    public static DateOnly D(int year, int month, int day) => new(year, month, day);

    public static InspectionResult Calc(
        VehicleType type,
        DisplacementBracket? bracket,
        DateOnly manufacture,
        DateOnly issue,
        DateOnly? licence = null,
        DateOnly? today = null,
        RuleTable? rules = null) =>
        InspectionCalculator.Calculate(
            new InspectionInput(type, bracket, manufacture, issue, licence),
            today ?? Today,
            rules ?? RuleTable.Current());

    public static InspectionItem Car(DateOnly manufacture, DateOnly issue, DateOnly? licence = null, DateOnly? today = null) =>
        Assert.Single(Calc(VehicleType.Car, DisplacementBracket.Car1201To1800, manufacture, issue, licence, today).Items);

    public static InspectionItem Item(InspectionResult result, InspectionKind kind) =>
        Assert.Single(result.Items, i => i.Kind == kind);

    public static void AssertWindow(
        InspectionItem item,
        InspectionKind kind,
        InspectionStatus status,
        DateOnly earliest,
        DateOnly deadline,
        InspectionBasis basis,
        bool estimated,
        string ruleId)
    {
        Assert.Equal(kind, item.Kind);
        Assert.Equal(status, item.Status);
        Assert.Equal(earliest, item.EarliestDate);
        Assert.Equal(deadline, item.Deadline);
        Assert.Equal(basis, item.Basis);
        Assert.Equal(estimated, item.Estimated);
        Assert.Equal(ruleId, item.RuleId);
    }

    public static VehicleDetails Details(
        string plate = "ABC-1234",
        string brand = "Toyota",
        string model = "Altis",
        string? nickname = null,
        string? color = null,
        VehicleType type = VehicleType.Car,
        DisplacementBracket? bracket = DisplacementBracket.Car1201To1800,
        DateOnly? manufacture = null,
        DateOnly? issue = null,
        DateOnly? licence = null) =>
        new(plate, brand, model, nickname, color, type, bracket,
            manufacture ?? D(2019, 6, 1), issue ?? D(2019, 7, 15), licence);
}
