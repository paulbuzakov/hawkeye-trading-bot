using HTB.MarketData.Shared.Abstractions;
using HTB.MarketData.Shared.Domain;

namespace HTB.MarketData.Shared.Persistence;

/// <summary>
/// EF Core / PostgreSQL read implementation of <see cref="ICandleRepository"/>. Runs on the
/// no-tracking <see cref="MarketDataReadonlyDbContext"/>, so it only ever queries the candle store.
/// Candle writes (upserts) belong to the loader.
/// </summary>
public sealed class CandleRepository(MarketDataReadonlyDbContext db) : ICandleRepository
{
    private readonly MarketDataReadonlyDbContext _db = db;

    public async Task<IReadOnlyList<Candle>> GetRangeAsync(
        SymbolCode symbolCode,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .Candles.Where(c =>
                c.Symbol == symbolCode && c.Interval == interval && c.OpenTime >= from && c.OpenTime <= to
            )
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<Candle?> GetLatestAsync(
        SymbolCode symbolCode,
        Timeframe interval,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .Candles.Where(c => c.Symbol == symbolCode && c.Interval == interval)
            .OrderByDescending(c => c.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
