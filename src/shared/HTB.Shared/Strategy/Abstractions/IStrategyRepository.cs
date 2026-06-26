using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Persistence;

namespace HTB.Shared.Strategy.Abstractions;

/// <summary>
/// Read-only query gateway for the strategy registry. Lives in HTB.Shared so any consumer (backtest
/// selection, live-run selection, governance/audit) can read persisted strategy versions without
/// depending on the writer. Writes are the loader's concern and live there. Returns the persisted
/// <see cref="StrategyVersionRecord"/>; callers re-materialize a runnable definition by feeding its
/// stored documents back through <see cref="IStrategyLoader"/>.
/// </summary>
public interface IStrategyRepository
{
    /// <summary>The version with this natural key, or <c>null</c> if absent.</summary>
    Task<StrategyVersionRecord?> GetAsync(
        string strategyId,
        int versionNumber,
        CancellationToken cancellationToken = default
    );

    /// <summary>The highest version number for a strategy id, or <c>null</c> if none exist.</summary>
    Task<StrategyVersionRecord?> GetLatestAsync(
        string strategyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// All versions, optionally filtered by <paramref name="status"/>, ordered by strategy id then
    /// version number.
    /// </summary>
    Task<IReadOnlyList<StrategyVersionRecord>> ListAsync(
        StrategyStatus? status = null,
        CancellationToken cancellationToken = default
    );
}
