namespace HTB.Shared.MarketData;

/// <summary>
/// Persistence gateway for OHLCV candles. Writes are idempotent on the candle's
/// natural key so backfill and live ingestion can safely overlap.
/// </summary>
public interface ICandleRepository
{
    /// <summary>
    /// Inserts or updates the given candles keyed on
    /// (exchange_id, symbol_id, interval, open_time). Returns the number of rows written.
    /// </summary>
    Task<int> UpsertAsync(
        IReadOnlyCollection<Candle> candles,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns candles for a symbol/interval with open time in the inclusive
    /// <paramref name="from"/>..<paramref name="to"/> range, ordered by open time ascending.
    /// </summary>
    Task<IReadOnlyList<Candle>> GetRangeAsync(
        int symbolId,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns the most recent candle for a symbol/interval, or <c>null</c> if none exist.
    /// </summary>
    Task<Candle?> GetLatestAsync(
        int symbolId,
        Timeframe interval,
        CancellationToken cancellationToken = default
    );
}
