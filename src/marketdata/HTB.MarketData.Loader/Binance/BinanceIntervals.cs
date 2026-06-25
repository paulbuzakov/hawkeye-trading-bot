using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Binance;

/// <summary>
/// Maps the domain <see cref="Timeframe"/> enum to the interval codes Binance's REST API
/// expects (<c>1m</c>, <c>5m</c>, <c>1h</c>, …).
/// </summary>
public static class BinanceIntervals
{
    private static readonly IReadOnlyDictionary<Timeframe, string> Codes = new Dictionary<
        Timeframe,
        string
    >
    {
        [Timeframe.M1] = "1m",
        [Timeframe.M5] = "5m",
        [Timeframe.M15] = "15m",
        [Timeframe.H1] = "1h",
        [Timeframe.H4] = "4h",
        [Timeframe.D1] = "1d",
    };

    /// <summary>Returns the Binance interval code for <paramref name="timeframe"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The timeframe has no Binance mapping.</exception>
    public static string ToCode(Timeframe timeframe)
    {
        if (Codes.TryGetValue(timeframe, out var code))
        {
            return code;
        }

        throw new ArgumentOutOfRangeException(
            nameof(timeframe),
            timeframe,
            "No Binance interval code is defined for this timeframe."
        );
    }
}
