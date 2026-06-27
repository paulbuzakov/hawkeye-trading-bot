namespace HTB.MarketData.Shared.Domain;

public sealed class Exchange
{
    public ExchangeCode Code { get; set; }

    public string Name { get; set; } = string.Empty;
}
