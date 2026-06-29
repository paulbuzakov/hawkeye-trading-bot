namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// Monotonic version of a <see cref="StrategyId"/> (the <c>version</c> field in
/// <c>meta.json</c>). Versions are immutable, start at 1, and increment whenever a
/// strategy's rules or parameter envelope change in a way that affects results.
/// </summary>
public readonly record struct StrategyVersion
{
    public StrategyVersion(int value)
    {
        if (value < 1)
        {
            throw new StrategyDomainException("Strategy version must be a positive integer.");
        }

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString();
}
