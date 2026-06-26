using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Domain.Conditions;
using HTB.Shared.Strategy.Strategy.Indicators;

namespace HTB.Shared.Strategy.Strategy;

/// <summary>
/// An <see cref="IStrategy"/> that interprets a compiled <see cref="StrategyDefinition"/>: it walks
/// the condition trees on the decision bar and emits a <see cref="Signal"/>. Exits are evaluated
/// before entries so that, when flat-vs-in-position is ambiguous, the safer intent wins (the risk
/// layer reconciles the intent against the actual position).
/// </summary>
public sealed class DeclarativeStrategy : IStrategy
{
    // Fixed precedence: exits before entries (see the format doc §6.4).
    private static readonly (string RuleKey, Signal Signal)[] Precedence =
    [
        ("exit-long", Signal.CloseLong),
        ("exit-short", Signal.CloseShort),
        ("entry-long", Signal.OpenLong),
        ("entry-short", Signal.OpenShort),
    ];

    private readonly StrategyDefinition _definition;

    public DeclarativeStrategy(StrategyDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (!definition.IsRunnable)
        {
            throw new InvalidOperationException(
                "Strategy definition is not runnable (not active, or its rules hash is unverified)."
            );
        }
    }

    /// <inheritdoc />
    public Signal Evaluate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var (ruleKey, signal) in Precedence)
        {
            if (_definition.Rules.Rules.TryGetValue(ruleKey, out var condition)
                && condition.IsSatisfiedBy(context))
            {
                return signal;
            }
        }

        return Signal.Hold;
    }

    /// <summary>
    /// Convenience entry point: builds the indicator series over <paramref name="window"/> and
    /// evaluates the latest (most recent) closed bar as the decision bar.
    /// </summary>
    public Signal Evaluate(IReadOnlyList<Candle> window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return Evaluate(BuildContext(window));
    }

    /// <summary>
    /// Replays <paramref name="window"/> through fresh indicator instances to build the
    /// <see cref="EvaluationContext"/> for its most recent bar.
    /// </summary>
    public EvaluationContext BuildContext(IReadOnlyList<Candle> window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.Count == 0)
        {
            throw new ArgumentException("Evaluation window must contain at least one candle.", nameof(window));
        }

        var series = new Dictionary<string, IReadOnlyList<decimal>>(_definition.Rules.Indicators.Count);
        foreach (var spec in _definition.Rules.Indicators)
        {
            var indicator = IndicatorFactory.Create(spec);
            var values = new decimal[window.Count];
            for (var i = 0; i < window.Count; i++)
            {
                indicator.Add(window[i]);
                values[i] = indicator.Value;
            }

            series[spec.Id] = values;
        }

        return new EvaluationContext(window, window.Count - 1, series, _definition.Parameters);
    }
}
