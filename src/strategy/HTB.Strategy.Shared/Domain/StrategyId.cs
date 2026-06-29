namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// Stable identity of a strategy family (the <c>id</c> field in <c>meta.json</c>, e.g.
/// <c>rsi-movement</c>). Normalised to a lower-case slug so the on-disk value is
/// case-insensitive and matches the kebab-case ids used by strategy bundles.
/// </summary>
public readonly record struct StrategyId
{
    public StrategyId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StrategyDomainException("Strategy id must be a non-empty slug.");
        }

        Value = value.Trim().ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
