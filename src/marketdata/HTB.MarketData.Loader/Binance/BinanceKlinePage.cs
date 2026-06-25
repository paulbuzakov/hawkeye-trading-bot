namespace HTB.MarketData.Loader.Binance;

/// <summary>
/// One page of klines streamed from Binance — at most <see cref="BinanceMarketDataClient.Limit"/>
/// bars — together with <see cref="IsFinal"/>, which marks the page that completes the requested
/// range so callers can flush it and report 100% progress without peeking ahead.
/// </summary>
public sealed record BinanceKlinePage(IReadOnlyList<BinanceKline> Klines, bool IsFinal);
