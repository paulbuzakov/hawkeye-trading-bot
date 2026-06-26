using HTB.Shared.MarketData.Domain;
using Microsoft.EntityFrameworkCore;

namespace HTB.MarketData.Loader.Persistence;

/// <summary>
/// EF Core / PostgreSQL implementation of <see cref="ICandleWriter"/>. Upserts use
/// <c>INSERT ... ON CONFLICT</c> so re-running backfill and overwriting the still-forming
/// (non-closed) candle are both no-ops or in-place updates.
/// </summary>
public sealed class CandleWriter(MarketDataWriteDbContext db) : ICandleWriter
{
    private readonly MarketDataWriteDbContext _db = db;

    public async Task<int> UpsertAsync(
        IReadOnlyCollection<Candle> candles,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(candles);

        var affected = 0;
        foreach (var c in candles)
        {
            affected += await _db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO marketdata.candles
                    (exchange_id, symbol_id, interval, open_time,
                     open, high, low, close, volume, quote_volume, trade_count, is_closed)
                VALUES
                    ({c.ExchangeId}, {c.SymbolId}, {(short)c.Interval}, {c.OpenTime},
                     {c.Open}, {c.High}, {c.Low}, {c.Close}, {c.Volume}, {c.QuoteVolume},
                     {c.TradeCount}, {c.IsClosed})
                ON CONFLICT (exchange_id, symbol_id, interval, open_time)
                DO UPDATE SET
                    high = EXCLUDED.high,
                    low = EXCLUDED.low,
                    close = EXCLUDED.close,
                    volume = EXCLUDED.volume,
                    quote_volume = EXCLUDED.quote_volume,
                    trade_count = EXCLUDED.trade_count,
                    is_closed = EXCLUDED.is_closed,
                    ingested_at = now();
                """,
                cancellationToken
            );
        }

        return affected;
    }
}
