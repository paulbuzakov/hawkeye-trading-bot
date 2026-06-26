using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Strategy.Indicators;

/// <summary>
/// Exponential moving average of a candle field over a fixed <c>period</c>. Seeded with the simple
/// average of the first <c>period</c> bars, then smoothed with multiplier <c>2 / (period + 1)</c>.
/// <see cref="IsReady"/> is true once <c>period</c> bars have been fed.
/// </summary>
public sealed class Ema : IIndicator
{
    private readonly CandleField _source;
    private readonly int _period;
    private readonly decimal _multiplier;
    private int _count;
    private decimal _seedSum;

    public Ema(CandleField source, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _source = source;
        _period = period;
        _multiplier = 2m / (period + 1);
    }

    public decimal Value { get; private set; }

    public bool IsReady => _count >= _period;

    public void Add(Candle candle)
    {
        var value = CandleFields.ValueOf(_source, candle);
        _count++;

        if (_count < _period)
        {
            // Warming up: report the running average until the seed is complete.
            _seedSum += value;
            Value = _seedSum / _count;
        }
        else if (_count == _period)
        {
            _seedSum += value;
            Value = _seedSum / _period;
        }
        else
        {
            Value = ((value - Value) * _multiplier) + Value;
        }
    }
}
