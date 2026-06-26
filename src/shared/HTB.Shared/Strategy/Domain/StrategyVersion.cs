namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// One immutable-once-active version of a strategy. <see cref="Number"/> is the second half of the
/// natural key <c>(strategy-id, version-number)</c>; new logic is a new number, never an edit.
/// <see cref="RulesHash"/> binds the exact <c>rules.json</c> this version runs.
/// </summary>
public sealed record StrategyVersion(
    int Number,
    StrategyStatus Status,
    DateTimeOffset CreatedAt,
    string RulesHash
);
