using HTB.MarketData.Shared.Domain;

namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// A versioned strategy definition — the in-memory shape of a strategy bundle's
/// <c>meta.json</c>. Identity is the <see cref="VersionId"/> (<see cref="Id"/> + <see cref="Version"/>);
/// the remaining fields are descriptive metadata plus the market scope the strategy is
/// declared to trade. The trading rules and parameter envelope live alongside in the bundle's
/// <c>rules.json</c> and are modelled separately.
/// </summary>
public sealed class StrategyDefinition
{
    public StrategyId Id { get; set; }
    public StrategyVersion Version { get; set; }

    public StrategyVersionId VersionId => new(Id, Version);

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = [];

    public IReadOnlyList<ExchangeCode> Exchanges { get; set; } = [];
    public IReadOnlyList<SymbolCode> Symbols { get; set; } = [];
    public IReadOnlyList<Timeframe> Timeframes { get; set; } = [];

    /// <summary>Bars of history the strategy needs before its signals are valid.</summary>
    public int WarmupBars { get; set; }
}
