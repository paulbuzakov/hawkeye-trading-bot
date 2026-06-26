using HTB.Shared.MarketData.Domain;

namespace HTB.Shared.Strategy.Abstractions;

/// <summary>
/// An incremental, referentially-transparent derived series: streaming state, but the same input
/// candles always produce the same outputs (so a backtest equals live). Candles are fed in
/// chronological order via <see cref="Add"/>; <see cref="Value"/> is the indicator's output for the
/// most recently added bar and is only trustworthy once <see cref="IsReady"/> is <c>true</c>.
/// </summary>
public interface IIndicator
{
    /// <summary>Feeds the next closed candle and updates <see cref="Value"/>.</summary>
    void Add(Candle candle);

    /// <summary>The indicator's value for the most recently added bar.</summary>
    decimal Value { get; }

    /// <summary>True once enough candles have been fed for <see cref="Value"/> to be valid.</summary>
    bool IsReady { get; }
}
