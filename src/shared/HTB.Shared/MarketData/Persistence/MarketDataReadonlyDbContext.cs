namespace HTB.Shared.MarketData.Persistence;

/// <summary>
/// Read-only context for the candle store and the EF migration target (the snapshot and the
/// design-time factory bind to it). Queries default to no-tracking — HTB.Shared exposes only
/// read logic, so nothing here mutates state. Ingestion writes through the loader's own
/// <c>MarketDataWriteDbContext</c>. The relational mapping lives in
/// <see cref="MarketDataDbContextBase"/>, shared by both sides.
/// </summary>
public sealed class MarketDataReadonlyDbContext(
    DbContextOptions<MarketDataReadonlyDbContext> options
) : MarketDataDbContextBase(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}
