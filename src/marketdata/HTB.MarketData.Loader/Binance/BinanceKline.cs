namespace HTB.MarketData.Loader.Binance;

/// <summary>
/// One OHLCV kline as returned by Binance's <c>/api/v3/klines</c> endpoint, already decoded
/// from its positional JSON-array form into named, typed fields.
/// </summary>
public sealed record BinanceKline(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal QuoteVolume,
    int TradeCount,
    DateTimeOffset CloseTime
);
