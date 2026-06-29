namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// How much capital an entry commits — the <c>positionSizing</c> entry in a <c>rules.json</c>
/// <c>risk</c> block. The meaning of <see cref="Value"/> depends on <see cref="Method"/> (a fraction
/// for <see cref="SizingMethod.PercentEquity"/>, a notional for <see cref="SizingMethod.FixedNotional"/>);
/// either way it must be strictly positive.
/// </summary>
public sealed record PositionSizing
{
    public PositionSizing(SizingMethod method, decimal value)
    {
        if (value <= 0m)
        {
            throw new StrategyDomainException($"Position sizing value must be positive (was {value}).");
        }

        Method = method;
        Value = value;
    }

    /// <summary>How the size is computed.</summary>
    public SizingMethod Method { get; }

    /// <summary>The sizing magnitude, interpreted per <see cref="Method"/>.</summary>
    public decimal Value { get; }
}
