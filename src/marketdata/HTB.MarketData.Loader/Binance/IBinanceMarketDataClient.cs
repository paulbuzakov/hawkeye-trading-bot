using HTB.MarketData.Shared.Domain;

namespace HTB.MarketData.Loader.Binance;

/// <summary>
/// Read-only access to Binance public market data: pair metadata and historical klines.
/// </summary>
public interface IBinanceMarketDataClient
{
    /// <summary>Returns the base/quote asset breakdown for <paramref name="ticker"/>.</summary>
    Task<BinanceSymbolInfo> GetSymbolInfoAsync(string ticker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams every closed kline for <paramref name="ticker"/> at <paramref name="interval"/>
    /// whose open time falls in the inclusive <paramref name="from"/>..<paramref name="to"/>
    /// range, one Binance page at a time and ordered by open time ascending. Paging lets callers
    /// persist and report progress incrementally instead of buffering the whole range. Always
    /// yields at least one page; the last page has <see cref="BinanceKlinePage.IsFinal"/> set.
    /// </summary>
    IAsyncEnumerable<BinanceKlinePage> StreamKlinesAsync(
        string ticker,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default
    );
}
