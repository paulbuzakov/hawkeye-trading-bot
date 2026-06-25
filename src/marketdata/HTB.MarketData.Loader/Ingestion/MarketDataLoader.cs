using HTB.MarketData.Loader.Binance;
using HTB.MarketData.Loader.Configuration;
using HTB.Shared.MarketData.Abstractions;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Ingestion;

/// <summary>
/// Drives a backfill: for every <see cref="SymbolLoadSpec"/> it resolves the Binance exchange
/// and symbol rows, pulls klines for each requested timeframe, and upserts them as
/// <see cref="Candle"/>s. Re-running is safe — every write is idempotent on the candle's
/// natural key.
/// </summary>
public sealed class MarketDataLoader(
    IBinanceMarketDataClient client,
    IInstrumentRepository instruments,
    ICandleRepository candles,
    TimeProvider timeProvider,
    Action<string>? log = null
)
{
    private const string ExchangeCode = "binance";
    private const string ExchangeName = "Binance";

    private readonly IBinanceMarketDataClient _client = client;
    private readonly IInstrumentRepository _instruments = instruments;
    private readonly ICandleRepository _candles = candles;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly Action<string> _log = log ?? (_ => { });

    /// <summary>
    /// Loads candles for every spec and returns the total number of candle rows written.
    /// </summary>
    public async Task<int> LoadAsync(
        IReadOnlyCollection<SymbolLoadSpec> specs,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(specs);

        var exchange = await _instruments.GetOrCreateExchangeAsync(
            ExchangeCode,
            ExchangeName,
            cancellationToken
        );

        var total = 0;
        foreach (var spec in specs)
        {
            total += await LoadSymbolAsync(exchange.Id, spec, cancellationToken);
        }

        return total;
    }

    private async Task<int> LoadSymbolAsync(
        int exchangeId,
        SymbolLoadSpec spec,
        CancellationToken cancellationToken
    )
    {
        var info = await _client.GetSymbolInfoAsync(spec.Ticker, cancellationToken);
        var symbol = await _instruments.GetOrCreateSymbolAsync(
            exchangeId,
            info.BaseAsset,
            info.QuoteAsset,
            info.Symbol,
            cancellationToken
        );

        var to = spec.To ?? _timeProvider.GetUtcNow();
        var written = 0;
        foreach (var timeframe in spec.Timeframes)
        {
            // Resume from the last stored bar so restarting the loader only fetches new candles
            // instead of re-downloading the whole range. Re-reading that one bar is harmless —
            // the upsert is idempotent. With no prior data we backfill from the requested start.
            var latest = await _candles.GetLatestAsync(symbol.Id, timeframe, cancellationToken);
            var from = latest?.OpenTime ?? spec.From;

            // Flush every page (up to ~1000 bars) as it arrives so a long backfill is persisted
            // incrementally and progress is visible, instead of buffering the whole range.
            var loaded = 0;
            var stream = _client.StreamKlinesAsync(
                spec.Ticker,
                timeframe,
                from,
                to,
                cancellationToken
            );

            await foreach (var page in stream.WithCancellation(cancellationToken))
            {
                var now = _timeProvider.GetUtcNow();
                // Skip the still-forming bar (its close time hasn't passed): only persist bars
                // that are final, so we never store a candle that can still change.
                var mapped = page
                    .Klines.Where(k => k.CloseTime < now)
                    .Select(k => ToCandle(exchangeId, symbol.Id, timeframe, k))
                    .ToList();

                loaded += await _candles.UpsertAsync(mapped, cancellationToken);

                // The final page (and an empty page, which carries no bar to measure against) is
                // 100%; intermediate pages are estimated from how much of the time window the last
                // bar covers.
                var percent =
                    page.IsFinal || page.Klines.Count == 0
                        ? 100
                        : PercentOfRange(from, to, page.Klines[^1].OpenTime);
                _log($"[{percent,3:0}%] {spec.Ticker} {timeframe}: {loaded} candles");
            }

            written += loaded;
        }

        return written;
    }

    /// <summary>
    /// Fraction (0..100) of the <paramref name="from"/>..<paramref name="to"/> window that ends at
    /// <paramref name="at"/>, used to estimate download progress before the final page arrives.
    /// </summary>
    private static decimal PercentOfRange(DateTimeOffset from, DateTimeOffset to, DateTimeOffset at)
    {
        var span = (to - from).Ticks;
        if (span <= 0)
        {
            return 100;
        }

        return Math.Round((decimal)Math.Clamp((at - from).Ticks * 100.00 / span, 0, 100), 3);
    }

    private static Candle ToCandle(
        int exchangeId,
        int symbolId,
        Timeframe interval,
        BinanceKline kline
    ) =>
        new()
        {
            ExchangeId = exchangeId,
            SymbolId = symbolId,
            Interval = interval,
            OpenTime = kline.OpenTime,
            Open = kline.Open,
            High = kline.High,
            Low = kline.Low,
            Close = kline.Close,
            Volume = kline.Volume,
            QuoteVolume = kline.QuoteVolume,
            TradeCount = kline.TradeCount,
            // Forming bars are filtered out before mapping, so every persisted bar is closed.
            IsClosed = true,
        };
}
