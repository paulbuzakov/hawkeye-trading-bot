namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// A technical indicator the strategy engine can compute. This is a closed set: adding a value
/// here is a commitment that the engine implements it. Stored as a stable numeric code; never
/// renumber existing members.
/// </summary>
public enum IndicatorKind : byte
{
    /// <summary>Relative Strength Index.</summary>
    Rsi = 1,

    /// <summary>Exponential Moving Average.</summary>
    Ema = 2,
}
