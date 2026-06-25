# Feature Specification: Backtest Runner

**Feature Branch**: `001-backtest-runner`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "I'd like to create backtest runner to test some trading strategies for the marketdata historical data"

## Clarifications

### Session 2026-06-25

- Q: How is a strategy defined — code or data? → A: A strategy is a **code algorithm (a strategy "type")** that ships with the bot (referenced by type name; a deterministic signal generator), parameterized by **stored configuration**. The algorithm is never stored; only configuration (parameter values, instrument binding, requested risk, execution settings) is stored as data. (Supersedes the earlier "declarative JSON ruleset fully replaces code" decision — strategies are NOT a code-free DSL; the runner does not interpret indicators/conditions from JSON.)
- Q: What does a strategy type expose, and how are parameters validated? → A: Each strategy type publishes a typed **parameter spec** (key, type, min/max/default), its required indicators, and required data inputs. A configuration's parameter values MUST validate against the type's spec (reject out-of-range/unknown values, or an unknown type) before any data is read (fail closed).
- Q: What does a stored configuration contain and where does it live? → A: An immutable, versioned document (with a config hash + change note) holding parameter values, the instrument binding (exchange, symbol, timeframe, precisions, min order size/notional), requested risk, and execution settings. Strategy types, configurations, versions, and results live in a **separate Strategy bounded context** in `HTB.Shared` with its own database context/schema/migrations, isolated from the market-data context. The same configuration drives backtest and future live trading ("tested = traded").
- Q: How does a backtest run obtain its configuration? → A: By **id + version** reference to a stored configuration, or an **inline** configuration (registered as a version before the run). The report records the fully resolved configuration either way.
- Q: What persistence scope is in v1? → A: Store, retrieve, and version configurations (create a new version, retrieve by id + version, list); each version is immutable. Full management API/UI is deferred.
- Q: Should each run's result be persisted and linked to the configuration version that produced it? → A: Yes — every run persists a result record linked to the exact configuration **id + version**; a version may have **many** result records (one per run). Listing a strategy shows its configuration versions, each with its result history (Strategy → Version → Results).
- Q: What does each persisted result record store? → A: The summary metrics, the run configuration (instrument, timeframe, date range, starting capital, fees, slippage, requested risk), and a run timestamp. The full trade-level report remains a separate run artifact.
- Q: Where are persisted results stored? → A: Within the Strategy bounded context, alongside configurations and versions.
- Q: What is in scope for the backtest runner vs deferred to live trading? → A: In scope: strategy type + parameters + instrument binding + requested risk (incl. stop-loss/take-profit) + modeled fees/slippage + period/starting capital. The stored model MAY carry live-only fields (paper/live status lifecycle, runtime state, live trade-history audit, order retry/idempotency/postOnly/time-in-force, schedule) but implementing their live **behavior** is OUT OF SCOPE for this feature.
- Q: Which performance metrics does the report produce? → A: The richer set — total return, annualized return, Sharpe ratio, Sortino ratio, max drawdown, win rate, profit factor, total trades, and average trade duration.
- Q: How is risk authority modeled? → A: The configuration's risk block is a set of **requested** constraints; the **shared central risk layer is the final authority** and may veto or resize any order, in backtest and live (constitution capital-safety). Configured stop-loss and take-profit are simulated in the backtest.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a strategy over historical data and get a performance report (Priority: P1)

A strategy author selects a strategy type and provides its configuration (parameters, instrument
binding, requested risk) — by reference to a stored version or inline — and runs a backtest over a
historical date range. The runner replays the stored historical candles in chronological order,
lets the configured strategy algorithm decide when to enter and exit, simulates the resulting
trades against a starting capital, and produces a performance report describing how the strategy
would have performed.

**Why this priority**: This is the core of the feature. Without the ability to replay history
through a strategy and see the outcome, nothing else has value. It is the minimum that lets a
user answer "would this strategy have made money?"

**Independent Test**: Can be fully tested by pointing the runner at a known instrument,
timeframe, and date range already present in the historical store, running one simple strategy,
and confirming a report is produced with a trade list, an ending balance, and summary metrics
(total return, max drawdown, win rate, number of trades).

**Acceptance Scenarios**:

1. **Given** historical candles exist for an instrument/timeframe over the requested range,
   **When** the user runs a backtest of a strategy with a starting capital, **Then** the runner
   produces a report containing the list of simulated trades, the ending balance, and summary
   performance metrics.
2. **Given** a strategy that never produces a trade signal, **When** the backtest runs over a
   valid range, **Then** the report shows zero trades and an ending balance equal to the
   starting capital.
3. **Given** the requested range contains candles, **When** the backtest completes, **Then**
   every reported trade references candle times that fall within the requested range and in
   chronological order.

---

### User Story 2 - Risk limits enforced during simulation (Priority: P2)

When the runner simulates a strategy's orders, it applies the same risk controls that govern
live trading — maximum position size, maximum daily loss, and a kill-switch. Orders that would
breach a limit are rejected in the simulation exactly as they would be in live trading, so the
backtest reflects what the system would actually have been allowed to do.

**Why this priority**: A backtest that ignores risk limits over-states performance and gives a
false picture of live behavior. The project's capital-safety principle requires every order
path to pass risk checks; the simulated order path is no exception. It builds on US1 but is
separable: US1 can run with limits effectively unbounded.

**Independent Test**: Can be tested by configuring a low maximum position size (or a daily-loss
limit) and a strategy that tries to exceed it, then confirming the report shows the offending
orders were rejected/clamped and that the kill-switch halts further trading once its threshold
is reached.

**Acceptance Scenarios**:

1. **Given** a maximum position size, **When** the strategy attempts an order that would exceed
   it, **Then** the order is rejected (or reduced to the limit, per configuration) and the
   rejection is recorded in the report.
2. **Given** a maximum daily loss limit, **When** cumulative losses in a single day reach the
   limit, **Then** no further entries are simulated for the remainder of that day and the event
   is recorded.
3. **Given** the kill-switch threshold is reached, **When** any subsequent order signal occurs,
   **Then** no further trades are simulated for the rest of the run and the halt is recorded in
   the report.

---

### User Story 3 - Realistic cost modeling (fees and slippage) (Priority: P3)

The user configures trading fees and slippage so simulated fills reflect real-world execution
costs rather than ideal prices. Each simulated trade's proceeds account for the configured fee
and price slippage.

**Why this priority**: Zero-cost backtests systematically over-state returns. Modeling fees and
slippage makes results credible and comparable to live expectations. It is additive on top of
US1 and can be defaulted to zero cost when not needed.

**Independent Test**: Can be tested by running the same strategy/range twice — once with zero
fees/slippage and once with non-zero values — and confirming the second run's net result is
lower by an amount consistent with the configured costs.

**Acceptance Scenarios**:

1. **Given** a non-zero fee rate, **When** a trade is simulated, **Then** the trade's recorded
   cost includes the fee applied to the traded value.
2. **Given** a non-zero slippage setting, **When** an order fills, **Then** the fill price is
   adjusted against the trader by the configured slippage relative to the reference price.
3. **Given** fees and slippage are both zero, **When** a backtest runs, **Then** results match
   the ideal-fill baseline from US1.

---

### User Story 4 - Reproducible, configurable runs (Priority: P4)

The user specifies all backtest inputs (instrument, timeframe, date range, starting capital,
risk limits, fees, slippage, strategy parameters) through a single configuration, and the runner
produces identical results every time the same configuration is run against the same data.

**Why this priority**: Determinism is required by the project constitution and is what makes
backtests trustworthy and comparable across runs. It is the lowest priority because US1–US3
deliver value first, but it hardens the feature for real decision-making.

**Independent Test**: Can be tested by running the same configuration twice and confirming the
two reports are byte-for-byte equivalent in their numeric results.

**Acceptance Scenarios**:

1. **Given** a fixed configuration and unchanged historical data, **When** the backtest is run
   twice, **Then** both runs produce identical trades, balances, and metrics.
2. **Given** a configuration that references a strategy and its parameters, **When** the runner
   starts, **Then** it records the full configuration alongside the results so a run can be
   reproduced later.

---

### Edge Cases

- **No data in range**: The requested instrument/timeframe/range has no stored candles — the run
  ends with a clear "no data" outcome rather than a misleading empty-but-successful report.
- **Partial data / gaps**: The range has missing candles (gaps in history) — the runner reports
  the gaps and continues over available candles without fabricating prices.
- **Range with a single candle**: Too few candles to form a trade — the run completes with zero
  trades and a clear note.
- **Insufficient balance**: The strategy signals an entry larger than the available balance —
  the order is rejected or resized; the simulation never goes below zero balance.
- **Stop-loss / take-profit hit**: Price reaches the configured stop-loss or take-profit while a
  position is open — the position is closed at that level (stop-loss takes precedence when both are
  reachable within the same candle), and the exit is recorded.
- **Open position at end of range**: A position is still open when data runs out — it is valued
  (marked to the last candle) and reported as an open/forced-close position, consistently.
- **Look-ahead protection**: A strategy must only ever see candles at or before the current
  simulated time; the runner must not expose future candles to the strategy.
- **Invalid configuration**: Start date after end date, unknown instrument, unsupported
  timeframe, negative balance/limit, or an invalid strategy configuration — rejected before the
  run with a clear validation error.
- **Invalid configuration document**: Schema-invalid JSON, unknown strategy type, or a parameter
  outside its type's spec range — rejected before any data is read, with a message identifying the
  fault.
- **Unknown configuration reference**: A run references a configuration id + version not present in
  the Strategy store — rejected before any data is read with a clear "configuration not found" error.
- **Inline configuration result linkage**: A run that supplies an inline configuration MUST still
  link its persisted result to a concrete configuration id + version — the runner registers the
  inline configuration as a new version in the Strategy store before recording the result, so no
  result is orphaned.
- **Indicator warm-up**: A strategy type requires an indicator before enough candles exist to
  compute it (e.g. a 200-period EMA in the first 199 candles) — the strategy does not signal on the
  incomplete indicator until it is available.
- **Conflicting signals**: A strategy type's entry and exit conditions both fire on the same candle
  — resolved by the type's documented, deterministic exit-precedence rule (a hard stop-loss always
  wins).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The runner MUST read historical candles for a specified instrument, timeframe, and
  inclusive date range from the existing historical market-data store.
- **FR-002**: The runner MUST replay candles in chronological (ascending open-time) order and
  present them to a strategy one step at a time, never revealing candles dated after the current
  simulated time (no look-ahead).
- **FR-003**: A strategy MUST be modeled as a code-defined strategy **type** (a deterministic
  signal-generating algorithm that ships with the bot, referenced by type name) parameterized by a
  stored **configuration**. The algorithm is never stored; only configuration (parameter values,
  instrument binding, requested risk, execution settings) is stored as data. The runner MUST
  instantiate the strategy type named by the configuration and drive it with the configured
  parameters. (Replaces the prior declarative-DSL/interpreter model; the runner does not interpret
  indicators or conditions from JSON.)
- **FR-003a**: Each strategy type MUST publish a typed parameter spec (key, type, min/max/default)
  and its required indicators/data inputs. The runner MUST validate a configuration's parameter
  values against the type's spec and reject out-of-range or unknown parameters, or an unknown
  strategy type, before any data is read (fail closed), consistent with FR-012.
- **FR-003b**: Strategy types, configurations, and the configuration validation/instantiation
  service MUST live in the shared library so the identical configuration drives both the backtest
  runner and future live trading; configuration documents MUST carry a schema version.
- **FR-003c**: Indicators required by a strategy type MUST be computed deterministically from the
  candles seen so far (no look-ahead, per FR-002); while a required indicator lacks enough candles
  (warm-up), the strategy MUST NOT signal on the incomplete indicator.
- **FR-003d**: The strategy domain MUST be a separate, self-contained context within the shared
  library with its own isolated data store (independent schema and persistence), separate from the
  market-data store — no shared tables or cross-context data coupling.
- **FR-003e**: The Strategy store MUST support persisting a configuration with a stable identity
  and versioning, retrieving a configuration by id + version, and listing configurations. A stored
  configuration version MUST be immutable; a change produces a new version (auditability).
- **FR-003f**: A backtest run MUST be able to reference a persisted configuration by id + version
  OR supply an inline configuration; both paths MUST pass identical validation (FR-003a). A
  reference to a configuration id/version not present in the store MUST be rejected before any data
  is read (fail closed).
- **FR-003g**: The configuration's risk block expresses **requested** constraints (max position
  size/percent, stop-loss, take-profit, max daily loss, kill-switch drawdown, cooldown). The
  shared **central risk layer is the final authority** and MAY veto or resize any order (this is
  the data/configuration view of the risk model; the runner-behavior view is FR-005). The central
  risk layer enforces max position size, max daily loss, and the kill-switch; requested fields it
  does not model in v1 — **max open positions** (implicitly 1 for single-instrument runs) and
  **cooldown** — are advisory and not enforced in v1. Configured stop-loss/take-profit ARE
  simulated.
- **FR-003h**: The stored strategy model MAY include live-only fields (paper/live status
  lifecycle, runtime state, live trade-history audit, execution retry/idempotency/post-only/
  time-in-force, schedule); implementing their live **behavior** is OUT OF SCOPE for the backtest
  runner, which reads only the fields it needs and ignores the rest.
- **FR-004**: The runner MUST simulate the orders a strategy emits against a configurable
  starting capital, tracking position, cash, and equity over the run.
- **FR-005**: The runner MUST route every simulated order through the shared central risk layer,
  which is the final authority — enforcing maximum position size, maximum daily loss, and a
  kill-switch, and vetoing or resizing orders that would breach a limit (the configuration's risk
  block is advisory/requested only, per the risk model in FR-003g). The runner MUST also simulate
  the configuration's stop-loss and take-profit exits.
- **FR-006**: The runner MUST apply configurable trading fees and slippage to simulated fills,
  defaulting to zero when not specified.
- **FR-007**: The runner MUST produce a performance report including the list of simulated trades
  (entry/exit time, side, size, price, fees), ending balance, and the following metrics: total
  return, annualized return, Sharpe ratio, Sortino ratio, maximum drawdown, win rate, profit
  factor, total trades, and average trade duration.
- **FR-008**: The runner MUST record any risk-limit rejections, daily-loss halts, and
  kill-switch activations in the report.
- **FR-009**: The runner MUST perform all monetary and quantity calculations using exact decimal
  arithmetic (no binary floating point), so results are precise and deterministic.
- **FR-010**: The runner MUST produce identical results for identical configuration and
  unchanged input data (deterministic execution).
- **FR-011**: The runner MUST accept all run inputs through a single run configuration (instrument,
  timeframe, date range, starting capital, modeled fees and slippage, and either a reference to a
  persisted strategy configuration by id + version or an inline strategy configuration including
  its type, parameters, and requested risk) and record that run configuration — including the
  fully resolved strategy configuration — with the results.
- **FR-012**: The runner MUST validate the configuration before running and reject invalid input
  (e.g., start after end, unknown instrument, unsupported timeframe, negative balance/limits, or
  an invalid strategy configuration) with a clear error.
- **FR-013**: The runner MUST handle empty ranges, gaps, and end-of-range open positions per the
  Edge Cases section without producing misleading results.
- **FR-014**: The runner MUST emit structured logs and metrics for the run (candles processed,
  trades simulated, rejections, run duration, final metrics) for observability, without logging
  any secrets.
- **FR-015**: The runner MUST NOT place any real or live orders and MUST NOT require live
  exchange connectivity; it operates only on stored historical data.
- **FR-016**: After a completed run, the runner MUST persist a result record linked to the exact
  strategy configuration id + version that produced it. A configuration version MAY have many
  result records (one per run); result records are append-only (a re-run adds a new record, never
  overwrites).
- **FR-017**: A persisted result record MUST store the summary metrics (FR-007), the run
  configuration (instrument, timeframe, date range, starting capital, fees, slippage, requested
  risk), and a run timestamp. The full trade-level report remains a separate run artifact and is
  not required within the result record. The recorded timestamp is metadata and does not affect
  the deterministic numeric results (SC-002).
- **FR-018**: The Strategy store MUST allow listing a strategy's configuration versions and, for
  each version, its associated result records (Strategy → Version → Results). Result records are
  stored within the Strategy bounded context.

### Key Entities *(include if feature involves data)*

- **Run Configuration**: The complete, self-contained description of a run — instrument,
  timeframe, date range, starting capital, modeled fee/slippage settings, and the strategy
  configuration (by reference or inline).
- **Historical Candle**: An OHLCV bar (open/high/low/close/volume, open time, timeframe) for an
  instrument, sourced from the existing market-data store; the input indicators are computed from.
- **Strategy Type**: A code-defined, deterministic signal-generating algorithm that ships with the
  bot (referenced by type name / code class). Publishes a typed parameter spec (key, type,
  min/max/default), required indicators, and required data inputs. Not stored as data.
- **Strategy Configuration**: A stored, versioned, immutable document binding a Strategy Type to
  concrete parameter values, an instrument binding, requested risk, and execution settings (with a
  config hash and change note). The code-free, data part of a strategy; shared verbatim by backtest
  and future live trading. Has a version history.
- **Configuration Version**: A specific, immutable version of a Strategy Configuration (identified
  by configuration id + version) — the unit a backtest runs against and that result records link
  to; a version may relate to many result records.
- **Instrument Binding**: The market the configuration trades — exchange, symbol, timeframe, price
  and quantity precision, and minimum order size/notional.
- **Backtest Result Record**: A persisted summary of one run — summary metrics, the run
  configuration, and a run timestamp — linked to the configuration version that produced it.
  Append-only.
- **Strategy Store**: The dedicated, self-contained strategy context in the shared library — with
  its own isolated data store and schema, separate from the market-data store — that persists,
  versions, retrieves (by id + version), and lists strategy configurations, and stores and lists
  the result records linked to each version (Strategy → Version → Results).
- **Indicator**: A named, parameterized computation over candle history (e.g. RSI(14), EMA(200))
  required by a strategy type; computed deterministically with no look-ahead.
- **Strategy Factory**: The component (in the shared library) that validates a configuration's
  parameters against its type's spec and instantiates the named strategy type with those
  parameters; rejects unknown types or out-of-spec parameters. Reusable by both the backtest runner
  and future live execution.
- **Central Risk Layer**: The authoritative shared risk component that every order passes through;
  it may veto or resize orders against max position size, max daily loss, and the kill-switch. The
  configuration's risk block is advisory input to it.
- **Order Intent / Simulated Order**: A strategy's request to trade, which passes through risk
  checks and cost modeling to become a simulated fill (or a recorded rejection).
- **Simulated Trade**: A completed (or open) position with entry/exit times, side, size, prices,
  fees, and realized/unrealized result.
- **Portfolio / Account State**: The evolving cash, position, and equity during the run.
- **Backtest Report**: The output — trade list, equity progression, summary metrics, risk
  events, and the run configuration used. A summary of the report is persisted as a Backtest Result
  Record linked to the configuration version.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can run a backtest of a strategy over a chosen instrument, timeframe, and
  date range and obtain a complete performance report in a single command/run.
- **SC-002**: Running the same configuration against the same data twice produces identical
  numeric results 100% of the time.
- **SC-003**: 100% of simulated orders pass through the central risk layer; no simulated trade
  violates the effective maximum position size, daily-loss limit, or kill-switch, regardless of
  what the configuration requested.
- **SC-004**: For a configuration with non-zero fees/slippage, the net result is measurably lower
  than the zero-cost baseline by an amount equal to the modeled costs (within exact-arithmetic
  precision).
- **SC-005**: The runner processes a full year of one-minute candles for a single instrument
  (~525,600 candles) in a single run without manual intervention, with bounded memory and within a
  soft wall-clock budget of a few minutes on a typical developer machine.
- **SC-006**: 100% of invalid configurations are rejected before any simulation begins, with an
  actionable error message.
- **SC-007**: A reviewer can reconstruct exactly what a past run did from its recorded
  configuration and report alone, without access to the original command invocation.
- **SC-008**: A new strategy instance can be added and backtested by authoring only a configuration
  (selecting an existing strategy type and parameter values) — with no code changes — and the
  identical configuration is runnable by future live trading. (Adding a new strategy *algorithm/
  type* requires shipping code.)
- **SC-009**: A configuration can be stored, versioned, retrieved by id + version, and listed; a
  backtest that references a stored configuration produces results identical to running that same
  configuration inline.
- **SC-010**: After a run, its result (summary metrics + run configuration + timestamp) is
  persisted and retrievable through the configuration version, so a user can list a strategy's
  configuration versions and see each version's full result history (multiple runs accumulate, none
  overwritten).
- **SC-011**: The report and persisted result include the full metric set (total return,
  annualized return, Sharpe, Sortino, max drawdown, win rate, profit factor, total trades, average
  trade duration).

## Assumptions

- **Single instrument per run**: A backtest covers one instrument and one timeframe per run.
  Multi-instrument portfolio backtesting is out of scope for the first version.
- **Data source**: Historical candles are read from the existing market-data storage layer; this
  feature does not fetch new data from exchanges. Populating history is a separate, existing
  concern (the market-data loader).
- **Fill model**: To avoid look-ahead bias, orders signalled on a candle are assumed to fill at
  the next candle's open price (adjusted for slippage), unless a more specific model is chosen in
  a later iteration. This is a reasonable industry-standard default.
- **Order types in scope**: v1 supports market-style entries and the configuration's stop-loss and
  take-profit exits. Trailing stops, OCO, and other exotic order types are out of scope for v1.
- **Strategy definition & reuse**: A strategy is a code-defined *type* (algorithm) shipped with the
  bot, parameterized by stored *configuration* data. The strategy types, the configuration
  validation/instantiation service, and the configuration store live in the shared library so the
  identical configuration drives both backtest and future live trading. v1 ships at least one
  strategy type (e.g. RSI mean-reversion) plus a sample configuration. Adding a new strategy
  algorithm requires shipping code; adding a new strategy *instance* requires only configuration.
- **Strategy context & storage**: The strategy domain is a separate, self-contained context in the
  shared library with its own isolated data store and independent schema, separate from the
  market-data store. It is expected to use the same storage engine/deployment pattern as the
  market-data context (separate schema), and its connection configuration is sourced from
  environment/secrets like all other data access (constitution: Security First). (The
  implementation realizes this as a distinct bounded context with its own database context and
  migrations — see Clarifications.)
- **Scope boundary (live-only fields)**: The stored strategy model may carry fields used only by
  live trading — paper/live status lifecycle, runtime state, live trade-history audit log, order
  retry/idempotency/post-only/time-in-force, and schedule. This feature (the backtest runner) reads
  only the fields it needs and does NOT implement their live behavior; that is deferred to the live
  trading feature.
- **Risk authority & reuse**: The configuration's risk block is a set of *requested* constraints.
  The shared central risk layer is the final authority and may veto or resize any order; it is the
  same component future live execution uses, so backtest and live risk behavior stay consistent
  (constitution capital-safety). Configured stop-loss/take-profit are simulated in the backtest.
- **Reporting form**: The report is produced in a structured, machine-readable form plus a
  human-readable summary; exact presentation (file vs. console) is an implementation detail
  decided during planning.
- **Result persistence**: Every completed run's result summary is persisted in the Strategy
  context, linked to the configuration version that produced it, and accumulated as append-only
  history (a version may have many results). Runs that supply an inline configuration first register
  it as a version so their results are never orphaned; this means ad-hoc inline runs add versions to
  the store. The full trade-level report is retained as a separate run artifact, not inside the
  persisted result record.
- **Determinism**: Given the constitution's determinism requirement, all financial math uses
  exact decimal arithmetic and the run order is fully deterministic.
- **No live connectivity**: The runner never contacts a live exchange and places no real orders.
