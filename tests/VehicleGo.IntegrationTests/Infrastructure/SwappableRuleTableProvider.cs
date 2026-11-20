using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Inspections.Rules;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>Replaceable rule table (design §1.3). Tests in the API collection run one at a time; reset after use.</summary>
public sealed class SwappableRuleTableProvider : IRuleTableProvider
{
    public RuleTable Rules { get; set; } = RuleTable.Current();

    public RuleTable GetRuleTable() => Rules;

    public void Reset() => Rules = RuleTable.Current();
}
