using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Strategy.Indicators;

/// <summary>
/// Wilder's Relative Strength Index of a candle field over a fixed <c>period</c>. The first
/// <c>period</c> price changes seed the average gain/loss with a simple average; subsequent bars
/// use Wilder smoothing. RSI is in <c>[0, 100]</c> (100 when there are no losses).
/// <see cref="IsReady"/> is true once <c>period</c> price changes (i.e. <c>period + 1</c> bars)
/// have been fed.
/// </summary>
public sealed class Rsi : IIndicator
{
    private readonly CandleField _source;
    private readonly int _period;
    private bool _hasPrevious;
    private decimal _previous;
    private int _deltaCount;
    private decimal _avgGain;
    private decimal _avgLoss;

    public Rsi(CandleField source, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _source = source;
        _period = period;
    }

    public decimal Value { get; private set; }

    public bool IsReady => _deltaCount >= _period;

    public void Add(Candle candle)
    {
        var value = CandleFields.ValueOf(_source, candle);
        if (!_hasPrevious)
        {
            _hasPrevious = true;
            _previous = value;
            return;
        }

        var change = value - _previous;
        _previous = value;
        var gain = change > 0 ? change : 0m;
        var loss = change < 0 ? -change : 0m;
        _deltaCount++;

        if (_deltaCount <= _period)
        {
            // Seed phase: simple average of the gains/losses seen so far.
            _avgGain += (gain - _avgGain) / _deltaCount;
            _avgLoss += (loss - _avgLoss) / _deltaCount;
        }
        else
        {
            // Wilder smoothing.
            _avgGain = ((_avgGain * (_period - 1)) + gain) / _period;
            _avgLoss = ((_avgLoss * (_period - 1)) + loss) / _period;
        }

        Value = _avgLoss == 0m ? 100m : 100m - (100m / (1m + (_avgGain / _avgLoss)));
    }
}
