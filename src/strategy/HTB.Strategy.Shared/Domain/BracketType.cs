namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// How a protective bracket (stop-loss / take-profit) distance is expressed. Stored as a stable
/// numeric code; never renumber.
/// </summary>
public enum BracketType : byte
{
    /// <summary>A fraction of the entry price (value in (0, 1]).</summary>
    Percent = 1,

    /// <summary>A multiple of Average True Range at entry.</summary>
    Atr = 2,
}
