using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Persistence;

/// <summary>
/// The persisted form of one loaded strategy version in the <c>strategy</c> schema. Stores the exact
/// <c>meta.json</c> + <c>rules.json</c> text (the bytes whose SHA-256 is <see cref="RulesHash"/>) plus
/// denormalized projection columns for registry queries. Re-materializing a runnable
/// <see cref="StrategyDefinition"/> is done by feeding <see cref="MetaJson"/>/<see cref="RulesJson"/>
/// back through the loader, so the loader stays the single source of truth and the hash stays the
/// integrity guarantee. Natural key: <c>(StrategyId, VersionNumber)</c>; immutable once active.
/// </summary>
public sealed class StrategyVersionRecord
{
    /// <summary>Stable strategy slug — first half of the natural key.</summary>
    public string StrategyId { get; set; } = string.Empty;

    /// <summary>Monotonic version number — second half of the natural key.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Human-readable name from the manifest.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Lifecycle state; stored as a stable <see cref="short"/> code.</summary>
    public StrategyStatus Status { get; set; }

    /// <summary>The strategy-format schema version (e.g. "1.0").</summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary><c>sha256:&lt;hex&gt;</c> of <see cref="RulesJson"/>; binds this version's algorithm.</summary>
    public string RulesHash { get; set; } = string.Empty;

    /// <summary>Primary decision timeframe; stored as a stable <see cref="short"/> code.</summary>
    public Timeframe Timeframe { get; set; }

    /// <summary>Warm-up bars the engine discards before signals are valid.</summary>
    public int WarmupBars { get; set; }

    /// <summary>The exact manifest document.</summary>
    public string MetaJson { get; set; } = string.Empty;

    /// <summary>The exact algorithm document; its SHA-256 must equal <see cref="RulesHash"/>.</summary>
    public string RulesJson { get; set; } = string.Empty;

    /// <summary>When the version was authored (from the manifest), UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the version was persisted into the registry, UTC.</summary>
    public DateTimeOffset RegisteredAt { get; set; }
}
