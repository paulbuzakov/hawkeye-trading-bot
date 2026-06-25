namespace HTB.MarketData.Loader.Binance;

/// <summary>
/// The asset breakdown of a Binance trading pair, taken from <c>/api/v3/exchangeInfo</c>.
/// Used to populate <see cref="HTB.Shared.MarketData.Symbol"/> base/quote columns.
/// </summary>
public sealed record BinanceSymbolInfo(string Symbol, string BaseAsset, string QuoteAsset);
