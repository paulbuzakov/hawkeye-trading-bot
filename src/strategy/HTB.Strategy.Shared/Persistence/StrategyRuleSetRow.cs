using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Persistence;

/// <summary>
/// Persistence row for a strategy's rules — the EF Core entity behind the
/// <c>strategy.strategy_rule_sets</c> table. Identity is the composite <see cref="Id"/> +
/// <see cref="Version"/>, which is also the foreign key to the owning
/// <see cref="StrategyDefinition"/> (the EF Core 1:1 shared-primary-key pattern); the rule tree is
/// held as a single jsonb document in <see cref="Rules"/>, keeping the deep, union-typed aggregate
/// out of the relational schema. Use <see cref="From"/> and <see cref="ToDomain"/> to cross the
/// domain ↔ storage boundary.
/// </summary>
public sealed class StrategyRuleSetRow
{
    /// <summary>The strategy family these rules belong to; first half of the table key.</summary>
    public StrategyId Id { get; set; }

    /// <summary>The strategy version these rules belong to; second half of the table key.</summary>
    public StrategyVersion Version { get; set; }

    /// <summary>The serialized rule body stored in the <c>rules</c> jsonb column.</summary>
    public string Rules { get; set; } = string.Empty;

    /// <summary>The <c>{id}@{version}</c> these rules belong to; computed from the key, not mapped.</summary>
    public StrategyVersionId VersionId => new(Id, Version);

    /// <summary>The definition (<c>meta.json</c>) these rules belong to; the principal of the 1:1.</summary>
    public StrategyDefinition Definition { get; set; } = null!;

    /// <summary>Projects a domain <see cref="StrategyRuleSet"/> into its storage row.</summary>
    public static StrategyRuleSetRow From(StrategyRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return new StrategyRuleSetRow
        {
            Id = rules.VersionId.Id,
            Version = rules.VersionId.Version,
            Rules = StrategyRuleSetSerializer.Serialize(rules),
        };
    }

    /// <summary>Reconstructs the domain <see cref="StrategyRuleSet"/> from this row.</summary>
    public StrategyRuleSet ToDomain() => StrategyRuleSetSerializer.Deserialize(VersionId, Rules);
}
