using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Persistence;

/// <summary>
/// Write gateway for OHLCV candles. Writes are idempotent on the candle's natural key so
/// backfill and live ingestion can safely overlap. Reads live in HTB.Shared's
/// <see cref="HTB.Shared.MarketData.Abstractions.ICandleRepository"/>.
/// </summary>
public interface ICandleWriter
{
    /// <summary>
    /// Inserts or updates the given candles keyed on
    /// (exchange_id, symbol_id, interval, open_time). Returns the number of rows written.
    /// </summary>
    Task<int> UpsertAsync(
        IReadOnlyCollection<Candle> candles,
        CancellationToken cancellationToken = default
    );
}
