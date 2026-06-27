namespace HTB.MarketData.Shared.Domain;

public sealed class Symbol
{
    public SymbolCode Code { get; set; }
    public ExchangeCode Exchange { get; set; }

    public string BaseAsset { get; set; } = string.Empty;
    public string QuoteAsset { get; set; } = string.Empty;
}
