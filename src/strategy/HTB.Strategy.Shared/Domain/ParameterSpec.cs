namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// One tunable strategy parameter together with its optimization envelope — the shape of an
/// entry in the <c>parameters</c> block of a bundle's <c>rules.json</c>. The <see cref="Default"/>
/// is the value used for a normal run; <see cref="Min"/>/<see cref="Max"/>/<see cref="Step"/>
/// bound a backtest sweep. Invariants are enforced at construction so an invalid envelope is
/// unconstructable: <c>min ≤ default ≤ max</c> and <c>step &gt; 0</c>.
/// </summary>
public sealed record ParameterSpec
{
    public ParameterSpec(string name, decimal @default, decimal min, decimal max, decimal step)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StrategyDomainException("Parameter name must be a non-empty string.");
        }

        if (min > max)
        {
            throw new StrategyDomainException($"Parameter '{name}' has min {min} greater than max {max}.");
        }

        if (@default < min || @default > max)
        {
            throw new StrategyDomainException(
                $"Parameter '{name}' default {@default} is outside the envelope [{min}, {max}]."
            );
        }

        if (step <= 0m)
        {
            throw new StrategyDomainException($"Parameter '{name}' step must be positive (was {step}).");
        }

        Name = name.Trim();
        Default = @default;
        Min = min;
        Max = max;
        Step = step;
    }

    /// <summary>Parameter name as referenced by <c>$name</c> operands (e.g. <c>oversold</c>).</summary>
    public string Name { get; }

    /// <summary>Value used for a normal (non-sweep) run.</summary>
    public decimal Default { get; }

    /// <summary>Inclusive lower bound of the optimization sweep.</summary>
    public decimal Min { get; }

    /// <summary>Inclusive upper bound of the optimization sweep.</summary>
    public decimal Max { get; }

    /// <summary>Increment between sweep samples; strictly positive.</summary>
    public decimal Step { get; }
}
