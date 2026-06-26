using HTB.Shared.Strategy.Domain.Conditions;

namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The parsed, validated, and <em>compiled</em> <c>rules.json</c>: the indicators a strategy
/// derives, its entry/exit condition trees (keyed by rule name, e.g. <c>entry-long</c>), and the
/// advisory risk and execution hints. <see cref="Rules"/> values are the compiled DSL ASTs the
/// engine walks per bar.
/// </summary>
public sealed record StrategyRules(
    string SchemaVersion,
    string StrategyId,
    int VersionNumber,
    IReadOnlyList<IndicatorSpec> Indicators,
    IReadOnlyDictionary<string, ICondition> Rules,
    RequestedRisk? RequestedRisk,
    ExecutionHints? Execution
);
