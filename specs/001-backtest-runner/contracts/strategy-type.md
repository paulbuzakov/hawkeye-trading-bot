# Contract: Strategy Type (code) + Parameter Spec

**Namespace**: `HTB.Shared.Strategy` · **Satisfies**: FR-003, FR-003a/c · **Ref**: R2, R3, R4

A strategy *type* is a code algorithm shipped with the bot — a deterministic signal generator. It
self-describes its tunable parameters and required indicators so configurations can be validated
before a run. There is **no JSON interpreter**; the algorithm is code.

```csharp
public interface IStrategyType
{
    string TypeName { get; }                      // stable id a configuration references
    string Category { get; }                      // e.g. "mean-reversion"
    IReadOnlyList<ParameterDef> ParameterSpec { get; }
    IReadOnlyList<IndicatorRequirement> RequiredIndicators { get; }
    IReadOnlyList<string> RequiredDataInputs { get; }   // e.g. close, volume

    // Build the executable strategy from validated parameter values.
    IStrategy Create(ParameterSet parameters);
}

public sealed record ParameterDef(string Key, ParamType Type, decimal? Min, decimal? Max, object Default);

public interface IStrategy   // the executable, instantiated strategy
{
    OrderIntent? OnCandle(IStrategyContext context);   // zero/one intent per candle; pure
}

public interface IStrategyContext
{
    Candle Current { get; }                  // just-closed decision candle
    IReadOnlyList<Candle> History { get; }   // candles up to and including Current
    PortfolioSnapshot Portfolio { get; }     // read-only account state
}
```

**Rules**
- `IStrategy.OnCandle` MUST be deterministic (no wall-clock/RNG) and look-ahead-free — the context
  cannot expose candles after `Current` (FR-002).
- Required indicators are computed in `decimal` from candles seen so far; during warm-up the
  strategy MUST NOT signal on an incomplete indicator (FR-003c).
- v1 ships at least one type: **RsiMeanReversion** (RSI oversold entry with EMA trend filter; exits
  on RSI overbought, configured stop-loss, or take-profit).
