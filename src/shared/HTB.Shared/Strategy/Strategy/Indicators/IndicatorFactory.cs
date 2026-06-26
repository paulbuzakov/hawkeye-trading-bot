using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Strategy.Indicators;

/// <summary>
/// The closed registry of indicator <c>kind</c>s. New indicators are a deliberate, tested addition
/// here — not arbitrary user code. An unknown kind is a load error.
/// </summary>
public static class IndicatorFactory
{
    /// <summary>The set of recognised indicator kinds (DSL spelling).</summary>
    public static IReadOnlySet<string> KnownKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "rsi", "ema", "sma" };

    /// <summary>True if <paramref name="kind"/> is a recognised indicator kind.</summary>
    public static bool IsKnown(string kind) => KnownKinds.Contains(kind);

    /// <summary>Creates a fresh indicator instance for the given resolved <see cref="IndicatorSpec"/>.</summary>
    public static IIndicator Create(IndicatorSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Kind switch
        {
            "rsi" => new Rsi(spec.Source, spec.Period),
            "ema" => new Ema(spec.Source, spec.Period),
            "sma" => new Sma(spec.Source, spec.Period),
            _ => throw new StrategyConfigException($"Unknown indicator kind \"{spec.Kind}\"."),
        };
    }
}
