namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>
/// A reference to a candle field (e.g. <c>close</c>) or an indicator id (e.g. <c>rsi</c>), read
/// <see cref="Offset"/> closed bars before the decision bar. A bare DSL string is offset 0; the
/// object form <c>{ "series": "rsi", "offset": 1 }</c> reads further back. <see cref="Offset"/> is
/// unsigned, so look-ahead is structurally impossible.
/// </summary>
public sealed class SeriesOperand : IOperand
{
    public SeriesOperand(string name, int offset)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        Name = name;
        Offset = offset;
    }

    public string Name { get; }

    public int Offset { get; }

    public decimal Evaluate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetSeries(Name, Offset);
    }
}
