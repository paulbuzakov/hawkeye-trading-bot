namespace HTB.Shared.MarketData.Domain;

/// <summary>
/// A tradable instrument (trading pair) on a specific <see cref="Exchange"/>.
/// </summary>
public sealed class Symbol
{
    public int Id { get; set; }

    public int ExchangeId { get; set; }

    /// <summary>Base asset, e.g. "BTC".</summary>
    public string BaseAsset { get; set; } = string.Empty;

    /// <summary>Quote asset, e.g. "USDT".</summary>
    public string QuoteAsset { get; set; } = string.Empty;

    /// <summary>The venue's own symbol string, e.g. "BTCUSDT".</summary>
    public string ExchangeSymbol { get; set; } = string.Empty;
}
