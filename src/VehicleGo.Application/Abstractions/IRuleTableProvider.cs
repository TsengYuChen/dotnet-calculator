using VehicleGo.Domain.Inspections.Rules;

namespace VehicleGo.Application.Abstractions;

/// <summary>
/// Supplies the rule table to the rule engine (design §1.3). The default implementation
/// (Infrastructure) returns <see cref="RuleTable.Current"/>; tests can supply a table with a rule missing.
/// </summary>
public interface IRuleTableProvider
{
    RuleTable GetRuleTable();
}
