using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Persistence;

namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// Write gateway for the strategy registry. Persists a loaded <see cref="StrategyDefinition"/> (with
/// its exact source documents) idempotently on the natural key <c>(strategy-id, version-number)</c>:
/// re-saving identical bytes is a no-op/promotion, while saving different bytes under an existing
/// active/retired version is rejected. Mirrors the loader-owned write side of market data.
/// </summary>
public interface IStrategyStore
{
    /// <summary>
    /// Saves <paramref name="definition"/> along with the exact <paramref name="manifestJson"/> and
    /// <paramref name="rulesJson"/> it was loaded from, and returns the persisted record. Throws
    /// <see cref="StrategyRegistryConflictException"/> if it would mutate an immutable version.
    /// </summary>
    Task<StrategyVersionRecord> SaveAsync(
        StrategyDefinition definition,
        string manifestJson,
        string rulesJson,
        CancellationToken cancellationToken = default
    );
}
