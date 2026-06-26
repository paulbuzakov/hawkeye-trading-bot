namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// A validated, ready-to-run strategy: its <see cref="Manifest"/>, its compiled
/// <see cref="StrategyRules"/>, and the resolved parameter values (name → bound value) the rules
/// reference. <see cref="IsRunnable"/> is true only when the version is <see cref="StrategyStatus.Active"/>
/// and its <c>rules-hash</c> was verified — a draft loads (for authoring) but must not be run
/// (see the format doc §9.4).
/// </summary>
public sealed record StrategyDefinition(
    StrategyManifest Manifest,
    StrategyRules Rules,
    IReadOnlyDictionary<string, decimal> Parameters,
    bool IsRunnable
);
