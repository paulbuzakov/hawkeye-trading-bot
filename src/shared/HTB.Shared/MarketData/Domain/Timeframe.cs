namespace HTB.Shared.MarketData.Domain;

/// <summary>
/// Candle (kline) timeframe, named with the common financial shorthand (minutes/hours/days).
/// Stored as a <see cref="short"/> code so the on-disk value is stable and independent of
/// declaration order.
/// </summary>
public enum Timeframe : short
{
    M1 = 1,
    M5 = 5,
    M15 = 15,
    H1 = 60,
    H4 = 240,
    D1 = 1440,
}
