using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Persistence;

/// <summary>
/// Persistence row for a strategy's rules — the EF Core entity behind the
/// <c>strategy.strategy_rule_sets</c> table. Identity is the <see cref="VersionId"/> (stored as
/// <c>{id}@{version}</c>); the rule tree is held as a single jsonb document in <see cref="Rules"/>,
/// keeping the deep, union-typed aggregate out of the relational schema. Use <see cref="From"/> and
/// <see cref="ToDomain"/> to cross the domain ↔ storage boundary.
/// </summary>
public sealed class StrategyRuleSetRow
{
    /// <summary>The <c>{id}@{version}</c> these rules belong to; the table key.</summary>
    public StrategyVersionId VersionId { get; set; }

    /// <summary>The serialized rule body stored in the <c>rules</c> jsonb column.</summary>
    public string Rules { get; set; } = string.Empty;

    /// <summary>Projects a domain <see cref="StrategyRuleSet"/> into its storage row.</summary>
    public static StrategyRuleSetRow From(StrategyRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return new StrategyRuleSetRow
        {
            VersionId = rules.VersionId,
            Rules = StrategyRuleSetSerializer.Serialize(rules),
        };
    }

    /// <summary>Reconstructs the domain <see cref="StrategyRuleSet"/> from this row.</summary>
    public StrategyRuleSet ToDomain() => StrategyRuleSetSerializer.Deserialize(VersionId, Rules);
}
