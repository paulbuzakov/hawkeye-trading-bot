using HTB.MarketData.Shared.Domain;

namespace HTB.MarketData.Shared.Abstractions;

/// <summary>
/// Read-only query gateway for OHLCV candles. Lives in HTB.Shared so any consumer (backtests,
/// analytics, the loader's resume logic) can read history. Writes are the loader's concern and
/// live there.
/// </summary>
public interface ICandleRepository
{
    /// <summary>
    /// Returns candles for a symbol/interval with open time in the inclusive
    /// <paramref name="from"/>..<paramref name="to"/> range, ordered by open time ascending.
    /// </summary>
    Task<IReadOnlyList<Candle>> GetRangeAsync(
        SymbolCode symbolCode,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns the most recent candle for a symbol/interval, or <c>null</c> if none exist.
    /// </summary>
    Task<Candle?> GetLatestAsync(
        SymbolCode symbolCode,
        Timeframe interval,
        CancellationToken cancellationToken = default
    );
}
