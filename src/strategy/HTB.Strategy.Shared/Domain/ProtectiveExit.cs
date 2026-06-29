namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// A protective bracket attached to an open position — a stop-loss or take-profit from a
/// <c>rules.json</c> <c>risk</c> block. <see cref="Value"/> is the distance, interpreted per
/// <see cref="Type"/>, and must be strictly positive.
/// </summary>
public sealed record ProtectiveExit
{
    public ProtectiveExit(BracketType type, decimal value)
    {
        if (value <= 0m)
        {
            throw new StrategyDomainException($"Protective exit value must be positive (was {value}).");
        }

        Type = type;
        Value = value;
    }

    /// <summary>How the distance is expressed.</summary>
    public BracketType Type { get; }

    /// <summary>The bracket distance, interpreted per <see cref="Type"/>.</summary>
    public decimal Value { get; }
}
