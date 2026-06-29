namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// A named indicator instance declared in a bundle's <c>rules.json</c> <c>indicators</c> block,
/// e.g. <c>"emaSlow": { "type": "EMA", "period": "$emaSlowPeriod", "source": "close" }</c>.
/// <see cref="Name"/> is how conditions reference it; <see cref="Period"/> is an
/// <see cref="Operand"/> so it can be a constant or a <c>$param</c> reference resolved at run time.
/// </summary>
public sealed record IndicatorSpec
{
    public IndicatorSpec(string name, IndicatorKind kind, Operand period, PriceSource source)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StrategyDomainException("Indicator name must be a non-empty string.");
        }

        ArgumentNullException.ThrowIfNull(period);

        Name = name.Trim();
        Kind = kind;
        Period = period;
        Source = source;
    }

    /// <summary>Name conditions reference this indicator by (e.g. <c>emaSlow</c>).</summary>
    public string Name { get; }

    /// <summary>Which indicator to compute.</summary>
    public IndicatorKind Kind { get; }

    /// <summary>Lookback period — a literal or a <c>$param</c> reference.</summary>
    public Operand Period { get; }

    /// <summary>Bar field the indicator is computed over.</summary>
    public PriceSource Source { get; }
}
