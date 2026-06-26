namespace HTB.Shared.Strategy.Persistence;

/// <summary>
/// Read-only context for the strategy registry and the EF migration target (the snapshot and the
/// design-time factory bind to it). Queries default to no-tracking — HTB.Shared exposes only read
/// logic, so nothing here mutates state. Registration writes through the loader's own
/// <c>StrategyWriteDbContext</c>. The relational mapping lives in <see cref="StrategyDbContextBase"/>,
/// shared by both sides. Mirrors <c>MarketDataReadonlyDbContext</c>.
/// </summary>
public sealed class StrategyReadonlyDbContext(DbContextOptions<StrategyReadonlyDbContext> options)
    : StrategyDbContextBase(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}
