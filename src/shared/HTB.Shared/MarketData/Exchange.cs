namespace HTB.Shared.MarketData;

/// <summary>
/// A trading venue (exchange) that market data is sourced from. Exchange-agnostic:
/// concrete venues are rows, not types.
/// </summary>
public sealed class Exchange
{
    public int Id { get; set; }

    /// <summary>Stable short code, e.g. "binance".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable name, e.g. "Binance".</summary>
    public string Name { get; set; } = string.Empty;
}
