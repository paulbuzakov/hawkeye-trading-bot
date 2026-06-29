namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// A field of a price bar — the <c>source</c> an indicator reads, and the bar value an
/// <see cref="Operand"/> can reference. Stored as a stable numeric code; never renumber.
/// </summary>
public enum PriceSource : byte
{
    Open = 1,
    High = 2,
    Low = 3,
    Close = 4,
    Volume = 5,
}
