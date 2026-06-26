using HTB.Shared.MarketData.Domain;

namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The parsed, validated <c>meta.json</c>: a strategy's identity, version, lifecycle, applicability,
/// and bounded parameter spec. This is the governance-controlled half of a strategy definition;
/// the trading logic lives in <see cref="StrategyRules"/>.
/// </summary>
public sealed record StrategyManifest(
    string SchemaVersion,
    string Id,
    string Name,
    string Description,
    string Author,
    bool Deterministic,
    StrategyVersion Version,
    Applicability Applicability,
    IReadOnlyDictionary<string, ParameterSpec> Parameters,
    IReadOnlyList<string> Tags
);

/// <summary>
/// Where a strategy may run: the (config-level) exchanges/symbols, its primary decision
/// <see cref="Timeframe"/>, and the warm-up bars the engine discards before signals are valid.
/// </summary>
public sealed record Applicability(
    IReadOnlyList<string> Exchanges,
    IReadOnlyList<string> Symbols,
    Timeframe Timeframe,
    int WarmupBars
);
