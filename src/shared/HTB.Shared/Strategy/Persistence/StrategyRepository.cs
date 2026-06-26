using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Persistence;

/// <summary>
/// EF Core / PostgreSQL read implementation of <see cref="IStrategyRepository"/>. Runs on the
/// no-tracking <see cref="StrategyReadonlyDbContext"/>, so it only ever queries the registry.
/// Registration (writes) belong to the loader. Mirrors <c>CandleRepository</c>.
/// </summary>
public sealed class StrategyRepository(StrategyReadonlyDbContext db) : IStrategyRepository
{
    private readonly StrategyReadonlyDbContext _db = db;

    public async Task<StrategyVersionRecord?> GetAsync(
        string strategyId,
        int versionNumber,
        CancellationToken cancellationToken = default
    )
    {
        return await _db.StrategyVersions.FirstOrDefaultAsync(
            s => s.StrategyId == strategyId && s.VersionNumber == versionNumber,
            cancellationToken
        );
    }

    public async Task<StrategyVersionRecord?> GetLatestAsync(
        string strategyId,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .StrategyVersions.Where(s => s.StrategyId == strategyId)
            .OrderByDescending(s => s.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StrategyVersionRecord>> ListAsync(
        StrategyStatus? status = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .StrategyVersions.Where(s => status == null || s.Status == status)
            .OrderBy(s => s.StrategyId)
            .ThenBy(s => s.VersionNumber)
            .ToListAsync(cancellationToken);
    }
}
