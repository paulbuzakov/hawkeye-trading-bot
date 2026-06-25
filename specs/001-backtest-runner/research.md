# Phase 0 Research: Backtest Runner

The spec carries no `[NEEDS CLARIFICATION]` markers (five clarification sessions settled the
strategy model). Decisions below derive from the spec's Clarifications/Assumptions, the
constitution, and existing codebase conventions.

## R1. Fill / execution model (look-ahead avoidance)

- **Decision**: Orders signalled while processing candle *N* fill at the **open of candle N+1**,
  adjusted for slippage; strategy types/indicators only see candles up to *N*.
- **Rationale**: Next-bar-open is the standard, deterministic, look-ahead-free choice (FR-002).
- **Alternatives**: same-bar-close (look-ahead biased); intrabar/tick replay (no sub-candle data).

## R2. Strategy model — code algorithm (type) + stored configuration

- **Decision**: A strategy is a **code-defined strategy type** (a deterministic signal-generating
  algorithm shipped with the bot, referenced by type name) parameterized by a **stored
  configuration** (parameter values, instrument binding, requested risk, execution settings). The
  algorithm is never stored; only configuration is data. The runner instantiates the named type and
  drives it with the configured parameters. **No JSON interpreter.**
- **Rationale**: Matches the user-provided strategy document ("ALGORITHM is code… only
  CONFIGURATION is stored as data"). Adding a strategy *instance* is data-only (SC-008); adding a
  new *algorithm* ships code. Code types keep complex/deterministic logic testable and fast.
- **Alternatives**: declarative JSON ruleset interpreter (earlier draft) — explicitly superseded by
  clarification; external plugin assemblies — arbitrary-code-execution risk (constitution I).

## R3. Strategy type contract & parameter validation

- **Decision**: Each strategy type publishes a typed **parameter spec** (key, type, min/max/default),
  its required indicators, and required data inputs. The **Strategy Factory** validates a
  configuration's parameter values against the type's spec and instantiates the type; unknown types
  or out-of-spec parameters are rejected **before any data is read** (fail closed, FR-003a).
- **Rationale**: A typed spec gives deterministic, fail-closed validation and self-describing types;
  the factory is the single creation path reused by backtest and live.
- **Alternatives**: untyped param bags — silent misconfiguration; rejected.

## R4. Indicators

- **Decision**: Indicators (SMA, EMA, RSI for v1) are computed in `decimal`, incrementally, from
  candles seen so far; during warm-up the strategy does not signal on an incomplete indicator
  (FR-003c).
- **Rationale**: Required by the shipped RSI mean-reversion type; deterministic and look-ahead-free.

## R5. Strategy persistence — separate bounded context + DbContext

- **Decision**: A dedicated **Strategy bounded context** in `HTB.Shared` with its own
  `StrategyDbContext` (PostgreSQL schema `strategy`) and a separate `HTB.Strategy.Migrations`
  project + design-time factory reading `HTB_STRATEGY_DB`. It persists configurations, immutable
  versions, and result records — isolated from the market-data context (no shared tables).
- **Rationale**: Required by the spec (FR-003d); mirrors the proven market-data
  persistence/migrations pattern; isolation keeps strategy definitions/outcomes independent and
  reusable by live execution.
- **Alternatives**: reuse the market-data schema/context — couples unrelated domains; rejected.

## R6. Configuration source resolution (stored ref vs inline)

- **Decision**: A run references a configuration by **id + version** or supplies an **inline**
  configuration. Both pass identical validation. An unknown reference is rejected before any data
  read. An inline configuration is **registered as a new version** before the run so its result
  links to a concrete id + version (no orphaned results, FR-003f).
- **Rationale**: Stored references give reproducibility/versioning; inline supports ad-hoc runs.

## R7. Result persistence — append-only, summary + config

- **Decision**: After each completed run, persist a **Backtest Result Record** linked to the
  configuration version: the full summary metric set, the run configuration, and a run timestamp.
  Append-only — a re-run adds a record, never overwrites. The full trade-level report is a separate
  run artifact.
- **Rationale**: Enables listing a strategy's versions with their result history (SC-010); compact;
  append-only is auditable. The timestamp is metadata, excluded from numeric results (FR-017).

## R8. Risk authority — central layer, config requests

- **Decision**: Every simulated order routes through the **shared central risk layer**
  (`CompositeRiskPolicy`), the final authority over max position size, max daily loss, and the
  kill-switch; it may veto or resize orders. The configuration's risk block is advisory input.
  Configured **stop-loss and take-profit** exits are simulated (stop-loss wins within a candle).
- **Rationale**: Constitution capital-safety requires every order path through the same risk checks;
  the central layer is the component future live execution reuses. SL/TP are part of the shipped
  strategy's behavior, so they are in v1 scope.
- **Alternatives**: enforce config values directly — diverges from live authority; rejected.

## R9. Performance metrics (full set)

- **Decision**: Report and persist: total return, annualized return, Sharpe ratio, Sortino ratio,
  max drawdown, win rate, profit factor, total trades, average trade duration — all `decimal`.
- **Rationale**: Matches the user-provided document and FR-007/SC-011; credible, comparable metrics.

## R10. Determinism (SC-002, FR-010)

- **Decision**: `decimal` throughout (incl. indicator and ratio math); fixed ascending replay order;
  no wall-clock or RNG in computation; ordered collections; full config echoed in the report. The
  persisted result timestamp is the only wall-clock value and is metadata only.
- **Rationale**: Removes the classic nondeterminism sources for byte-identical numeric results.
- **Note**: annualized return / Sharpe / Sortino use a fixed periods-per-year convention derived
  from the timeframe (documented), keeping them deterministic.

## R11. Performance / memory (SC-005)

- **Decision**: Single forward-pass replay; bounded state (portfolio, incremental metric
  accumulators, indicator rolling windows, trade/event log). `IBacktestDataSource` shaped to allow
  paged/streaming retrieval later. A large-range smoke test validates throughput.
- **Rationale**: ~525,600 candles/year is modest; forward-pass avoids O(n²) recomputation.

## R12. Report output form

- **Decision**: The engine returns a `BacktestReport` object; the CLI host serializes JSON + prints
  a summary; the engine also hands the summary to the result repository for persistence.
- **Rationale**: Keeps the engine pure/testable; persistence and presentation are host concerns.
