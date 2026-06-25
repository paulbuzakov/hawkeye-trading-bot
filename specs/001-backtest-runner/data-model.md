# Phase 1 Data Model: Backtest Runner

All monetary/quantity fields are `decimal`; timestamps are `DateTimeOffset` (UTC); stored aggregate
identities are `Guid`; types are immutable where practical. Two persistence concerns: **read-only**
market-data candles (existing) and the **new Strategy store** (schema `strategy`) holding strategy
configurations, versions, and result records. **Strategy types are code, not data.**

## Code (not persisted) — Strategy types

### StrategyType (code, in `HTB.Shared/Strategy`)

A deterministic signal-generating algorithm shipped with the bot. Discovered/registered by name;
never stored as data.

| Member | Type | Notes |
|--------|------|-------|
| TypeName | string | Stable identifier referenced by a configuration (e.g. "RsiMeanReversion"). |
| Category | string | e.g. "mean-reversion". |
| ParameterSpec | list of ParameterDef | Typed parameter definitions (below). |
| RequiredIndicators | list | Indicators the type needs (e.g. RSI bound to `rsiPeriod`, EMA to `trendFilterPeriod`). |
| RequiredDataInputs | list | e.g. open/high/low/close/volume. |
| (behavior) | — | Given an `IStrategyContext`, emits zero/one `OrderIntent` per candle. Pure, deterministic. |

### ParameterDef

| Field | Type | Notes |
|-------|------|-------|
| Key | string | e.g. "rsiPeriod". |
| Type | enum {Int, Decimal, Bool, String} | |
| Min / Max | decimal? | Inclusive bounds; null = unbounded. |
| Default | value | Used when a configuration omits the key. |

## Persisted aggregates (Strategy bounded context — schema `strategy`)

### StrategyConfiguration

The strategy-instance identity/aggregate root. Binds a strategy type to data. Has many versions.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Stable configuration identity. |
| Name | string | Human label; unique. |
| TypeName | string | The code strategy type this configuration parameterizes. |
| Status | enum {Paper, Live, Disabled} | Lifecycle label; lifecycle *behavior* is out of scope (FR-003h). |
| CreatedAt | DateTimeOffset | Metadata. |

### ConfigurationVersion (immutable)

A specific version of a configuration — the unit a backtest runs against and results link to.

| Field | Type | Notes |
|-------|------|-------|
| ConfigurationId | Guid (FK) | → StrategyConfiguration.Id. |
| VersionNumber | int | Monotonic per configuration; (ConfigurationId, VersionNumber) unique. |
| ParametersJson | string (jsonb) | Parameter values for the type (validated against its ParameterSpec). |
| InstrumentBinding | (embedded) | Exchange, symbol, timeframe, price/qty precision, min order size/notional. |
| RequestedRisk | (embedded) | maxPositionSize(Quote/PctEquity), maxOpenPositions, stopLossPct, takeProfitPct, maxDailyLoss, killSwitchDrawdownPct, cooldown — **advisory** input to the central risk layer. |
| ExecutionSettings | (embedded) | orderType, timeInForce, postOnly, slippageTolerance, retry, idempotencyKeyTemplate — carried; **live behavior out of scope** (FR-003h). |
| SchemaVersion | string | Configuration document grammar version. |
| ConfigHash | string | Content hash for integrity/dedup. |
| ChangeNote | string | Human note. |
| CreatedAt | DateTimeOffset | Metadata. |

**Invariant**: immutable once written; a change creates a new version (FR-003e). Inline
configurations are registered here before a run (R6).

### BacktestResultRecord (append-only)

A persisted summary of one run, linked to the version that produced it.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Record identity. |
| ConfigurationId + VersionNumber | FK | → ConfigurationVersion. |
| Instrument / Timeframe | string / Timeframe | From run config. |
| From / To | DateTimeOffset | Run range. |
| StartingCapital | decimal | |
| FeesModeled / SlippageModeled | decimal | Run config snapshot. |
| Metrics | (embedded PerformanceMetrics) | Full metric set (below). |
| RunTimestamp | DateTimeOffset | Metadata; NOT part of deterministic numeric results (FR-017). |

**Invariant**: append-only; many records per ConfigurationVersion (FR-016, SC-010).

## Risk types (in `HTB.Shared/Risk`)

### RiskLimits / OrderIntent / RiskDecision / RiskState

- **RiskLimits**: MaxPositionSize (decimal; 0/unset ⇒ unbounded), MaxDailyLoss, KillSwitchMaxDrawdown,
  OnBreach {Reject, Resize}. Derived from the configuration's RequestedRisk + global defaults; the
  central layer is authoritative.
- **OrderIntent**: Side {Buy, Sell}, Size (decimal > 0), SignalTime (UTC).
- **RiskDecision**: Outcome {Approved, Resized, Rejected, Halted}, ApprovedSize (decimal), Reason.
- **RiskState**: per-UTC-day realized loss, peak equity, kill-switch-tripped flag.

## Backtest runtime types (in `HTB.Backtest`)

### BacktestConfiguration (run inputs)

| Field | Type | Validation |
|-------|------|-----------|
| Instrument / ExchangeId | string / int | Resolves to symbol; must exist. |
| Timeframe | Timeframe | Defined enum value. |
| From / To | DateTimeOffset | From < To. |
| StartingCapital | decimal | > 0. |
| Fees / Slippage | FeeModel / SlippageModel | ≥ 0. |
| ConfigurationRef **or** InlineConfiguration | (id+version) **or** document | Exactly one; ref must exist; inline must validate (type known, params in spec). |

Validated before any data read (FR-012); fail closed.

### StrategyContext / SimulatedOrder / SimulatedTrade / Portfolio / RiskEvent

- **StrategyContext**: Current candle, History (≤ Current, no look-ahead), PortfolioSnapshot.
- **SimulatedOrder**: Side, FilledSize, FillPrice (next-bar open ± slippage), Fee, FillTime.
- **SimulatedTrade**: Side, Size, Entry/Exit time+price, Fees, RealizedPnl, ExitReason
  {Signal, StopLoss, TakeProfit, EndOfRange}, IsOpen.
- **Portfolio/PortfolioSnapshot**: Cash, PositionSize, AverageEntryPrice, Equity (decimal; cash
  never negative).
- **RiskEvent**: Time, Type {PositionSizeBreach, DailyLossHalt, KillSwitch}, Detail.

### PerformanceMetrics (full set — FR-007, SC-011)

| Field | Type | Definition |
|-------|------|-----------|
| TotalReturn | decimal | EndingEquity / StartingCapital − 1. |
| AnnualizedReturn | decimal | TotalReturn annualized via timeframe periods-per-year (R10). |
| SharpeRatio | decimal | Mean / stdev of periodic returns, annualized (fixed convention). |
| SortinoRatio | decimal | Mean / downside-deviation of periodic returns, annualized. |
| MaxDrawdown | decimal | Largest peak-to-trough equity decline. |
| WinRate | decimal | Winning / total closed trades (0 when none). |
| ProfitFactor | decimal | Gross profit / gross loss (∞-guard when no losses). |
| TotalTrades | int | Closed trades. |
| AvgTradeDurationHours | decimal | Mean holding time of closed trades. |

### BacktestReport

| Field | Type | Notes |
|-------|------|-------|
| Configuration | BacktestConfiguration | Echoed incl. resolved strategy configuration (SC-007). |
| ResolvedConfigurationVersion | (id + version) | The version the result record links to. |
| Outcome | enum {Completed, NoData, ValidationFailed} | |
| Starting/EndingBalance | decimal | |
| Trades / RiskEvents | ordered lists | Chronological. |
| Metrics | PerformanceMetrics | Full set. |
| DataGaps | list | Reported gaps (FR-013). |
| CandlesProcessed | int | Observability. |

## Relationships

```text
StrategyType (CODE) ──named by──> StrategyConfiguration (1) ──< ConfigurationVersion (many, immutable) ──< BacktestResultRecord (many, append-only)
BacktestConfiguration ──references──> ConfigurationVersion (by id+version, or inline→registered)
StrategyFactory: (StrategyType + ConfigurationVersion.parameters) ──validate+instantiate──> IStrategy
Candle (market-data, read-only) ──feeds──> Indicators ──> StrategyType ──> OrderIntent ──> CentralRiskLayer ──> FillSimulator(+SL/TP) ──> Portfolio
BacktestReport ──summarized into──> BacktestResultRecord
```
