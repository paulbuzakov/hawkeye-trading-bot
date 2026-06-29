namespace HTB.Strategy.Shared.Persistence;

/// <summary>
/// Read-only context for the strategy store and the EF migration target (the snapshot and the
/// design-time factory bind to it). Queries default to no-tracking. The relational mapping lives
/// in <see cref="StrategyDbContextBase"/>, shared with any future tracked write-side context.
/// </summary>
public sealed class StrategyReadonlyDbContext(DbContextOptions<StrategyReadonlyDbContext> options)
    : StrategyDbContextBase(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}
