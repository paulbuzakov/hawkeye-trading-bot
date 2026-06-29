namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// The in-memory shape of a strategy bundle's <c>rules.json</c> — the trading rules and parameter
/// envelope that accompany the <see cref="StrategyDefinition"/> (<c>meta.json</c>). Identity is the
/// <see cref="VersionId"/>, which a loader cross-checks against the definition so rules and metadata
/// always describe the same <c>{id}@{version}</c>.
/// </summary>
/// <remarks>
/// This is a pure declarative model: it validates structural invariants (no nulls, no duplicate
/// parameter or indicator names) but does not resolve <c>$param</c> / indicator references — that
/// cross-referencing is the rules parser's responsibility, since it needs the whole document.
/// </remarks>
public sealed record StrategyRuleSet
{
    public StrategyRuleSet(
        StrategyVersionId versionId,
        TradeDirection direction,
        IReadOnlyList<ParameterSpec> parameters,
        IReadOnlyList<IndicatorSpec> indicators,
        SignalRule entry,
        SignalRule exit,
        RiskRules risk
    )
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(indicators);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(risk);

        RequireDistinctNames(parameters.Select(p => p.Name), "parameter");
        RequireDistinctNames(indicators.Select(i => i.Name), "indicator");

        VersionId = versionId;
        Direction = direction;
        Parameters = parameters;
        Indicators = indicators;
        Entry = entry;
        Exit = exit;
        Risk = risk;
    }

    /// <summary>The <c>{id}@{version}</c> these rules belong to.</summary>
    public StrategyVersionId VersionId { get; }

    /// <summary>Side(s) the strategy may trade.</summary>
    public TradeDirection Direction { get; }

    /// <summary>Tunable parameters and their optimization envelopes; names are unique.</summary>
    public IReadOnlyList<ParameterSpec> Parameters { get; }

    /// <summary>Indicators the rules compute over; names are unique.</summary>
    public IReadOnlyList<IndicatorSpec> Indicators { get; }

    /// <summary>The entry signal.</summary>
    public SignalRule Entry { get; }

    /// <summary>The exit signal.</summary>
    public SignalRule Exit { get; }

    /// <summary>Position sizing, protective brackets, and concurrency caps.</summary>
    public RiskRules Risk { get; }

    private static void RequireDistinctNames(IEnumerable<string> names, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!seen.Add(name))
            {
                throw new StrategyDomainException($"Duplicate {kind} name '{name}' in rule set.");
            }
        }
    }
}
