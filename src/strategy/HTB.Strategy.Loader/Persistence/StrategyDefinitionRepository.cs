using HTB.Strategy.Shared.Domain;
using HTB.Strategy.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IStrategyDefinitionRepository"/>. Upserts a definition and
/// its rule set under the same <c>(id, version)</c> key: inserts when absent, otherwise refreshes
/// the definition's mutable descriptive fields and the rule set's jsonb body. Both rows commit in a
/// single <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> so loading a bundle is atomic
/// and idempotent.
/// </summary>
public sealed class StrategyDefinitionRepository(StrategyWriteDbContext db) : IStrategyDefinitionRepository
{
    private readonly StrategyWriteDbContext _db = db;

    public async Task<StrategySaveOutcome> SaveAsync(
        StrategyDefinition definition,
        StrategyRuleSet ruleSet,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(ruleSet);

        var id = definition.Id;
        var version = definition.Version;

        var existing = await _db.StrategyDefinitions.FirstOrDefaultAsync(
            d => d.Id == id && d.Version == version,
            cancellationToken
        );

        StrategySaveOutcome outcome;
        if (existing is null)
        {
            _db.StrategyDefinitions.Add(definition);
            outcome = StrategySaveOutcome.Inserted;
        }
        else
        {
            existing.Name = definition.Name;
            existing.Description = definition.Description;
            existing.Tags = definition.Tags;
            existing.Exchanges = definition.Exchanges;
            existing.Symbols = definition.Symbols;
            existing.Timeframes = definition.Timeframes;
            existing.WarmupBars = definition.WarmupBars;
            outcome = StrategySaveOutcome.Updated;
        }

        await UpsertRuleSetAsync(id, version, ruleSet, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return outcome;
    }

    private async Task UpsertRuleSetAsync(
        StrategyId id,
        StrategyVersion version,
        StrategyRuleSet ruleSet,
        CancellationToken cancellationToken
    )
    {
        var serialized = StrategyRuleSetRow.From(ruleSet);

        var existingRow = await _db.StrategyRuleSets.FirstOrDefaultAsync(
            r => r.Id == id && r.Version == version,
            cancellationToken
        );

        if (existingRow is null)
        {
            _db.StrategyRuleSets.Add(serialized);
        }
        else
        {
            existingRow.Rules = serialized.Rules;
        }
    }
}
