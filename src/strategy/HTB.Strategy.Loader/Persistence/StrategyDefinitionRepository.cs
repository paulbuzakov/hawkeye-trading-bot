using HTB.Strategy.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IStrategyDefinitionRepository"/>. Looks a definition up
/// by its <c>(id, version)</c> key and inserts it when missing; on a re-run for the same version
/// it refreshes the mutable descriptive fields so loading the same bundle is idempotent.
/// </summary>
public sealed class StrategyDefinitionRepository(StrategyWriteDbContext db) : IStrategyDefinitionRepository
{
    private readonly StrategyWriteDbContext _db = db;

    public async Task<StrategySaveOutcome> SaveAsync(
        StrategyDefinition definition,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(definition);

        var id = definition.Id;
        var version = definition.Version;
        var existing = await _db.StrategyDefinitions.FirstOrDefaultAsync(
            d => d.Id == id && d.Version == version,
            cancellationToken
        );

        if (existing is null)
        {
            _db.StrategyDefinitions.Add(definition);
            await _db.SaveChangesAsync(cancellationToken);
            return StrategySaveOutcome.Inserted;
        }

        existing.Name = definition.Name;
        existing.Description = definition.Description;
        existing.Tags = definition.Tags;
        existing.Exchanges = definition.Exchanges;
        existing.Symbols = definition.Symbols;
        existing.Timeframes = definition.Timeframes;
        existing.WarmupBars = definition.WarmupBars;
        await _db.SaveChangesAsync(cancellationToken);
        return StrategySaveOutcome.Updated;
    }
}
