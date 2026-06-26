using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IStrategyStore"/>. Looks the version up by its natural key
/// and applies the registry's immutability rule: absent → insert; present with the same
/// <c>rules-hash</c> → idempotent update of mutable fields (status promotion, name); present with a
/// different hash → overwrite only if the existing row is still a draft, otherwise reject. Uses the
/// tracked <see cref="StrategyWriteDbContext"/> (read-modify-decide), like <c>InstrumentRepository</c>.
/// </summary>
public sealed class StrategyStore(StrategyWriteDbContext db, TimeProvider clock) : IStrategyStore
{
    private readonly StrategyWriteDbContext _db = db;
    private readonly TimeProvider _clock = clock;

    public async Task<StrategyVersionRecord> SaveAsync(
        StrategyDefinition definition,
        string manifestJson,
        string rulesJson,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(manifestJson);
        ArgumentNullException.ThrowIfNull(rulesJson);

        var manifest = definition.Manifest;
        var id = manifest.Id;
        var version = manifest.Version.Number;

        var existing = await _db.StrategyVersions.FirstOrDefaultAsync(
            s => s.StrategyId == id && s.VersionNumber == version,
            cancellationToken
        );

        if (existing is null)
        {
            var record = new StrategyVersionRecord
            {
                StrategyId = id,
                VersionNumber = version,
                RegisteredAt = _clock.GetUtcNow(),
            };
            Apply(record, definition, manifestJson, rulesJson);
            _db.StrategyVersions.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
            return record;
        }

        var sameBytes = string.Equals(
            existing.RulesHash,
            manifest.Version.RulesHash,
            StringComparison.OrdinalIgnoreCase
        );
        if (!sameBytes && existing.Status != StrategyStatus.Draft)
        {
            throw new StrategyRegistryConflictException(
                $"strategy \"{id}\" version {version} is "
                    + $"{existing.Status.ToString().ToLowerInvariant()} and immutable; "
                    + "a change must be a new version number."
            );
        }

        // Idempotent re-save, draft overwrite, or status promotion: refresh in place.
        Apply(existing, definition, manifestJson, rulesJson);
        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private static void Apply(
        StrategyVersionRecord record,
        StrategyDefinition definition,
        string manifestJson,
        string rulesJson
    )
    {
        var manifest = definition.Manifest;
        record.Name = manifest.Name;
        record.Status = manifest.Version.Status;
        record.SchemaVersion = manifest.SchemaVersion;
        record.RulesHash = manifest.Version.RulesHash;
        record.Timeframe = manifest.Applicability.Timeframe;
        record.WarmupBars = manifest.Applicability.WarmupBars;
        record.MetaJson = manifestJson;
        record.RulesJson = rulesJson;
        record.CreatedAt = manifest.Version.CreatedAt;
    }
}
