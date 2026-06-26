using HTB.Shared.MarketData.Abstractions;
using HTB.Shared.MarketData.Domain;

namespace HTB.Shared.MarketData.Persistence;

/// <summary>
/// EF Core / PostgreSQL read implementation of <see cref="ICandleRepository"/>. Runs on the
/// no-tracking <see cref="MarketDataReadonlyDbContext"/>, so it only ever queries the candle store.
/// Candle writes (upserts) belong to the loader.
/// </summary>
public sealed class CandleRepository(MarketDataReadonlyDbContext db) : ICandleRepository
{
    private readonly MarketDataReadonlyDbContext _db = db;

    public async Task<IReadOnlyList<Candle>> GetRangeAsync(
        int symbolId,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .Candles.Where(c =>
                c.SymbolId == symbolId
                && c.Interval == interval
                && c.OpenTime >= from
                && c.OpenTime <= to
            )
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<Candle?> GetLatestAsync(
        int symbolId,
        Timeframe interval,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .Candles.Where(c => c.SymbolId == symbolId && c.Interval == interval)
            .OrderByDescending(c => c.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
