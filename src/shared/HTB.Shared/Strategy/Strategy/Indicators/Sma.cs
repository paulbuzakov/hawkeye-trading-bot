using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Strategy.Indicators;

/// <summary>
/// Simple moving average of a candle field over a fixed <c>period</c>. <see cref="IsReady"/> is
/// true once <c>period</c> bars have been fed; before that, <see cref="Value"/> is the running
/// average of the bars seen so far.
/// </summary>
public sealed class Sma : IIndicator
{
    private readonly CandleField _source;
    private readonly int _period;
    private readonly Queue<decimal> _window;
    private decimal _sum;

    public Sma(CandleField source, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _source = source;
        _period = period;
        _window = new Queue<decimal>(period);
    }

    public decimal Value { get; private set; }

    public bool IsReady => _window.Count >= _period;

    public void Add(Candle candle)
    {
        var value = CandleFields.ValueOf(_source, candle);
        _window.Enqueue(value);
        _sum += value;
        if (_window.Count > _period)
        {
            _sum -= _window.Dequeue();
        }

        Value = _sum / _window.Count;
    }
}
