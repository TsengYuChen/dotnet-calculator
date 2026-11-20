using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Inspections.Rules;

namespace VehicleGo.UnitTests.Fakes;

public sealed class FakeRuleTableProvider : IRuleTableProvider
{
    public RuleTable Rules { get; set; } = RuleTable.Current();

    public RuleTable GetRuleTable() => Rules;
}
