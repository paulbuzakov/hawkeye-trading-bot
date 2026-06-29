namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// How a position's size is determined. Stored as a stable numeric code; never renumber.
/// </summary>
public enum SizingMethod : byte
{
    /// <summary>A fraction of account equity (value in (0, 1]).</summary>
    PercentEquity = 1,

    /// <summary>A fixed quote-currency notional per position.</summary>
    FixedNotional = 2,
}
