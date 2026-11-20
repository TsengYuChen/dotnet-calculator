using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Inspections.Rules;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>The default rule table: <see cref="RuleTable.Current"/> (tests can replace the provider).</summary>
public sealed class RuleTableProvider : IRuleTableProvider
{
    public RuleTable GetRuleTable() => RuleTable.Current();
}
