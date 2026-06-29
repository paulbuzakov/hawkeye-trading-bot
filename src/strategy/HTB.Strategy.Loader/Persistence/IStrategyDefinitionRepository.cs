using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Loader.Persistence;

public interface IStrategyDefinitionRepository
{
    /// <summary>
    /// Persists <paramref name="definition"/> and its <paramref name="ruleSet"/> keyed by the same
    /// version id (<c>id + version</c>), in a single transaction: inserts new rows, or refreshes the
    /// definition's descriptive fields and the rule set's jsonb body for an existing version. The
    /// returned outcome reflects the definition (the principal of the 1:1 relationship).
    /// </summary>
    Task<StrategySaveOutcome> SaveAsync(
        StrategyDefinition definition,
        StrategyRuleSet ruleSet,
        CancellationToken cancellationToken = default
    );
}
