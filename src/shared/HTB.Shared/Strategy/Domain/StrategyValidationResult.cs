namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The verdict of a non-throwing strategy <em>check step</em>: did the strategy load, and is it
/// runnable? Returned by the loaders' <c>Validate</c> methods so a registry/governance caller can
/// admit or reject a strategy (and batch-check many) without try/catch. Invariant-by-construction:
/// a valid result carries a non-null <see cref="Definition"/> and no <see cref="Errors"/>; an invalid
/// result carries a null definition and at least one error. "Valid" means exactly "<c>Load</c> would
/// have succeeded" — the throwing loader stays the single source of truth.
/// </summary>
public sealed record StrategyValidationResult
{
    private StrategyValidationResult(bool isValid, StrategyDefinition? definition, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Definition = definition;
        Errors = errors;
    }

    /// <summary>True when the strategy parsed, validated, and hash-checked cleanly.</summary>
    public bool IsValid { get; }

    /// <summary>The loaded definition when <see cref="IsValid"/>; otherwise <c>null</c>.</summary>
    public StrategyDefinition? Definition { get; }

    /// <summary>The validation failures; empty when <see cref="IsValid"/>.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// True only when the strategy both loaded <em>and</em> is an active, hash-verified version that may
    /// be run live. A valid-but-draft strategy is <see cref="IsValid"/> yet not runnable.
    /// </summary>
    public bool IsRunnable => Definition?.IsRunnable ?? false;

    /// <summary>A passing verdict wrapping the loaded <paramref name="definition"/>.</summary>
    public static StrategyValidationResult Valid(StrategyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new StrategyValidationResult(true, definition, []);
    }

    /// <summary>A failing verdict carrying the first validation <paramref name="error"/>.</summary>
    public static StrategyValidationResult Invalid(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new StrategyValidationResult(false, null, [error]);
    }
}
