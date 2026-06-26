using HTB.Shared.MarketData.Domain;

namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>
/// Everything an operand or condition needs to evaluate one decision bar: the closed-candle window
/// (chronological), the index of the decision bar within it, the pre-computed indicator series
/// (id → values aligned to the window), and the resolved parameter values.
/// </summary>
/// <remarks>
/// Look-ahead is structurally impossible: an operand can only read the decision bar (offset 0) or
/// further into the past (offset ≥ 1). <see cref="ShiftBack"/> moves the decision point earlier so
/// cross operators can inspect the prior bar without operands hand-rolling the look-back.
/// </remarks>
public sealed class EvaluationContext
{
    private readonly IReadOnlyList<Candle> _window;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<decimal>> _indicatorSeries;
    private readonly IReadOnlyDictionary<string, decimal> _parameters;

    /// <summary>Index of the decision bar within the window. May be negative after a <see cref="ShiftBack"/> past the window start; reads then throw.</summary>
    public int DecisionIndex { get; }

    public EvaluationContext(
        IReadOnlyList<Candle> window,
        int decisionIndex,
        IReadOnlyDictionary<string, IReadOnlyList<decimal>> indicatorSeries,
        IReadOnlyDictionary<string, decimal> parameters
    )
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(indicatorSeries);
        ArgumentNullException.ThrowIfNull(parameters);
        if (window.Count == 0)
        {
            throw new ArgumentException("Evaluation window must contain at least one candle.", nameof(window));
        }

        if (decisionIndex >= window.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decisionIndex),
                decisionIndex,
                "Decision index points past the end of the window."
            );
        }

        _window = window;
        DecisionIndex = decisionIndex;
        _indicatorSeries = indicatorSeries;
        _parameters = parameters;
    }

    /// <summary>Returns a context whose decision bar is <paramref name="bars"/> earlier (same data).</summary>
    public EvaluationContext ShiftBack(int bars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bars);
        return new EvaluationContext(_window, DecisionIndex - bars, _indicatorSeries, _parameters);
    }

    /// <summary>
    /// Reads a candle field or indicator series <paramref name="offset"/> closed bars before the
    /// decision bar. <paramref name="offset"/> must be ≥ 0 (no look-ahead).
    /// </summary>
    public decimal GetSeries(string name, int offset)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        var index = DecisionIndex - offset;
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Insufficient history to read '{name}' at offset {offset}."
            );
        }

        if (CandleFields.TryParse(name, out var field))
        {
            return CandleFields.ValueOf(field, _window[index]);
        }

        if (_indicatorSeries.TryGetValue(name, out var series))
        {
            return series[index];
        }

        throw new KeyNotFoundException($"Unknown series '{name}'.");
    }

    /// <summary>Resolves a bound parameter value by name (without the leading <c>$</c>).</summary>
    public decimal GetParameter(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_parameters.TryGetValue(name, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException($"Unknown parameter '{name}'.");
    }
}
