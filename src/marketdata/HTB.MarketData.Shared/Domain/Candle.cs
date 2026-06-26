namespace HTB.MarketData.Shared.Domain;

/// <summary>
/// An OHLCV candle (kline) for a symbol/interval. The natural key is
/// (<see cref="ExchangeId"/>, <see cref="SymbolId"/>, <see cref="Interval"/>,
/// <see cref="OpenTime"/>). Prices and sizes are <see cref="decimal"/>; time is UTC.
/// </summary>
public sealed class Candle
{
    public int ExchangeId { get; set; }

    public int SymbolId { get; set; }

    public Timeframe Interval { get; set; }

    /// <summary>Bar open time (UTC). Part of the natural key and the hypertable partition column.</summary>
    public DateTimeOffset OpenTime { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal Volume { get; set; }

    /// <summary>Traded volume expressed in the quote asset.</summary>
    public decimal QuoteVolume { get; set; }

    public int TradeCount { get; set; }

    /// <summary>False while the bar is still forming; it is overwritten until it closes.</summary>
    public bool IsClosed { get; set; }

    /// <summary>When the row was last written. Defaulted by the database on insert/update.</summary>
    public DateTimeOffset IngestedAt { get; set; }
}
