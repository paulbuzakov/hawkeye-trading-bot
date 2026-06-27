namespace HTB.MarketData.Shared.Domain;

public readonly record struct SymbolCode
{
    public SymbolCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new MarketDataDomainException("Symbol code must be a non-empty slug.");
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
