---
description: "Task list for Backtest Runner implementation"
---

# Tasks: Backtest Runner

**Input**: Design documents from `/specs/001-backtest-runner/`

**Prerequisites**: plan.md, spec.md, data-model.md, contracts/ (strategy-type, strategy-factory,
strategy-store, risk, execution, data-source, report), research.md, quickstart.md

**Tests**: REQUIRED. The constitution mandates TDD for all trading, risk, and order-execution
logic, and the repo requires 100% line + branch coverage of `src/`. Test tasks are written first
and must fail before implementation (Red-Green-Refactor).

**Model**: A strategy is a **code-defined strategy type** (algorithm) parameterized by **stored,
versioned configuration** (data). The **Strategy Factory** validates a configuration's parameters
against its type's parameter spec and instantiates the type — there is no JSON interpreter. Every
order routes through the **authoritative central risk layer**; the configuration's risk block is
advisory. Money/quantity/ratio types are `decimal`; times are `DateTimeOffset` (UTC); stored ids
are `Guid`; types are sealed with primary constructors per repo conventions; unit tests use fakes;
store integration tests use Testcontainers.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US4; Setup/Foundational/Polish carry no story label
- Paths follow plan.md: `src/backtest/HTB.Backtest`, `src/backtest/HTB.Backtest.Runner`,
  `src/strategy/HTB.Strategy.Migrations`, `src/shared/HTB.Shared/{Risk,Strategy,Trading}`, tests
  under `tests/backtest/HTB.Backtest.Tests`, `tests/strategy/HTB.Strategy.Migrations.Tests`, and
  the existing `tests/shared/HTB.Shared.Tests`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project scaffolding and solution wiring.

- [ ] T001 Create `src/backtest/HTB.Backtest/HTB.Backtest.csproj` (net10.0, ImplicitUsings, Nullable) referencing `src/shared/HTB.Shared/HTB.Shared.csproj`
- [ ] T002 Create `src/backtest/HTB.Backtest.Runner/HTB.Backtest.Runner.csproj` (console; Microsoft.Extensions.DependencyInjection/Logging/Configuration) referencing `HTB.Backtest` and `HTB.Shared`
- [ ] T003 [P] Create `src/strategy/HTB.Strategy.Migrations/HTB.Strategy.Migrations.csproj` (Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2, Microsoft.EntityFrameworkCore.Design) referencing `HTB.Shared`, mirroring `HTB.MarketData.Migrations.csproj`
- [ ] T004 [P] Create `tests/backtest/HTB.Backtest.Tests/HTB.Backtest.Tests.csproj` (xunit, coverlet.collector, Microsoft.NET.Test.Sdk) referencing `HTB.Backtest`
- [ ] T005 [P] Create `tests/strategy/HTB.Strategy.Migrations.Tests/HTB.Strategy.Migrations.Tests.csproj` (xunit, coverlet, Testcontainers.PostgreSql) referencing `HTB.Strategy.Migrations`, mirroring `HTB.MarketData.Migrations.Tests`
- [ ] T006 Register all new projects in `HTB.slnx` under `/src/backtest/`, `/src/strategy/`, `/tests/backtest/`, `/tests/strategy/`
- [ ] T007 [P] Add shared `decimal` money/rounding helpers and `OrderSide`/`ExitReason` enums in `src/shared/HTB.Shared/Trading/` used by Risk, Strategy, and Backtest layers

**Checkpoint**: `dotnet build HTB.slnx` succeeds with empty projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting abstractions every user story depends on — the Risk and Strategy
contexts in `HTB.Shared`, indicators, the candle data source, and the run-configuration shape.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Risk context (HTB.Shared/Risk)

- [ ] T008 [P] Define risk domain types `RiskLimits`, `OrderIntent`, `RiskDecision` (Outcome {Approved,Resized,Rejected,Halted}), `RiskState` in `src/shared/HTB.Shared/Risk/Domain/`
- [ ] T009 [P] Define `IRiskPolicy` abstraction in `src/shared/HTB.Shared/Risk/Abstractions/IRiskPolicy.cs` (per contracts/risk.md)

### Strategy context — code types & abstractions (HTB.Shared/Strategy)

- [ ] T010 [P] Define `IStrategy` (executable) and `IStrategyContext` in `src/shared/HTB.Shared/Strategy/Abstractions/`
- [ ] T011 [P] Define `IStrategyType`, `ParameterDef`/`ParameterSet`, `IndicatorRequirement` (parameter spec contract) in `src/shared/HTB.Shared/Strategy/Abstractions/` (per contracts/strategy-type.md)
- [ ] T012 [P] Define the configuration domain model `StrategyConfiguration`, `ConfigurationVersion` (Guid ids, JSONB params), `InstrumentBinding`, `RequestedRisk`, `ExecutionSettings`, `BacktestResultRecord` in `src/shared/HTB.Shared/Strategy/Domain/`

### Indicators (HTB.Shared/Strategy/Domain/Indicators)

- [ ] T013 [P] Test indicator framework + SMA/EMA/RSI for determinism, decimal math, and no-look-ahead warm-up in `tests/shared/HTB.Shared.Tests/Strategy/IndicatorTests.cs`
- [ ] T014 Implement indicator framework + SMA, EMA, RSI (decimal, incremental, warm-up-aware) in `src/shared/HTB.Shared/Strategy/Domain/Indicators/` (makes T013 pass)

### Run configuration & data source (HTB.Backtest)

- [ ] T015 [P] Test `BacktestConfigurationValidator` (start<end, unknown instrument, unsupported timeframe, non-positive capital, unknown strategy type, out-of-spec parameter, exactly one of ref/inline) in `tests/backtest/HTB.Backtest.Tests/Configuration/BacktestConfigurationValidatorTests.cs`
- [ ] T016 Implement `BacktestConfiguration` + `BacktestConfigurationValidator` (fail closed) in `src/backtest/HTB.Backtest/Configuration/` (makes T015 pass)
- [ ] T017 [P] Test `RepositoryBacktestDataSource` ordering/closed-only/cancellation against a fake `ICandleRepository` in `tests/backtest/HTB.Backtest.Tests/Data/RepositoryBacktestDataSourceTests.cs`
- [ ] T018 Implement `IBacktestDataSource` + `RepositoryBacktestDataSource` (adapts `ICandleRepository.GetRangeAsync`) in `src/backtest/HTB.Backtest/Data/` (makes T017 pass)
- [ ] T019 [P] Add `FakeCandleRepository` test double in `tests/backtest/HTB.Backtest.Tests/FakeCandleRepository.cs`

**Checkpoint**: Foundation ready — abstractions, indicators, config validation, and data source exist and are tested.

---

## Phase 3: User Story 1 - Run a strategy over history and get a report (Priority: P1) 🎯 MVP

**Goal**: Replay candles through a configured strategy type and produce a full-metric performance
report.

**Independent Test**: Point the runner at a populated instrument/timeframe/range with an inline
configuration of a shipped strategy type and confirm a report with trades, ending balance, and the
full metric set is produced. Risk is pass-through and fills are ideal (fees/slippage = 0) at this
stage.

### Tests for User Story 1 (write first, must fail) ⚠️

- [ ] T020 [P] [US1] Test `StrategyFactory` validates parameters against the type spec and instantiates the named type (rejects unknown type / out-of-spec / wrong-type params; applies defaults) in `tests/shared/HTB.Shared.Tests/Strategy/StrategyFactoryTests.cs`
- [ ] T021 [P] [US1] Test the RSI mean-reversion strategy type produces deterministic entry/exit signals (RSI oversold entry + EMA trend filter; no-look-ahead; warm-up) in `tests/shared/HTB.Shared.Tests/Strategy/RsiMeanReversionStrategyTests.cs`
- [ ] T022 [P] [US1] Test `Portfolio` cash/position/equity updates and never-negative-cash in `tests/backtest/HTB.Backtest.Tests/Execution/PortfolioTests.cs`
- [ ] T023 [P] [US1] Test `PerformanceMetrics` — all 9 metrics (total return, annualized return, Sharpe, Sortino, max drawdown, win rate, profit factor, total trades, avg trade duration) on a known equity/trade series in `tests/backtest/HTB.Backtest.Tests/Reporting/PerformanceMetricsTests.cs`
- [ ] T024 [P] [US1] Test `BacktestEngine` end-to-end on a fake data source: produces report, no-signal ⇒ zero trades + balance unchanged, trades within range and chronological, `NoData` outcome on empty range in `tests/backtest/HTB.Backtest.Tests/Engine/BacktestEngineTests.cs`

### Implementation for User Story 1

- [ ] T025 [US1] Implement `StrategyFactory` (`IStrategyFactory`: validate params vs spec, resolve type by name, instantiate) in `src/shared/HTB.Shared/Strategy/StrategyFactory.cs` (makes T020 pass)
- [ ] T026 [US1] Implement the RSI mean-reversion strategy type (`IStrategyType` + `IStrategy`) in `src/shared/HTB.Shared/Strategy/Domain/Types/RsiMeanReversionStrategy.cs` (makes T021 pass)
- [ ] T027 [P] [US1] Implement `Portfolio` + `PortfolioSnapshot` in `src/backtest/HTB.Backtest/Execution/Portfolio.cs` (makes T022 pass)
- [ ] T028 [P] [US1] Implement ideal `IFillSimulator` (next-bar-open, zero cost) in `src/backtest/HTB.Backtest/Execution/NextBarOpenFillSimulator.cs`
- [ ] T029 [P] [US1] Implement `PerformanceMetrics` calculator (all 9 metrics, fixed periods-per-year convention from timeframe) in `src/backtest/HTB.Backtest/Reporting/PerformanceMetrics.cs` (makes T023 pass)
- [ ] T030 [US1] Implement `BacktestReport` (resolved configuration + `ResolvedConfigurationVersion` + outcome + `SimulatedTrade` incl. `ExitReason`) in `src/backtest/HTB.Backtest/Reporting/BacktestReport.cs`
- [ ] T031 [US1] Implement `PassThroughRiskPolicy` (approve-all) in `src/shared/HTB.Shared/Risk/PassThroughRiskPolicy.cs` so the order path exists in US1 (authoritative enforcement in US2)
- [ ] T032 [US1] Implement `BacktestEngine` replay loop (resolve config→factory→strategy, forward-pass, route orders via `IRiskPolicy`→`IFillSimulator`→`Portfolio`, gap detection, end-of-range open-position handling marked `EndOfRange`) in `src/backtest/HTB.Backtest/Engine/BacktestEngine.cs` (makes T024 pass)
- [ ] T033 [US1] Wire the CLI host (DI, parse run config incl. inline strategy configuration with type + parameters, run engine, serialize JSON report + print summary) in `src/backtest/HTB.Backtest.Runner/Program.cs`
- [ ] T034 [P] [US1] Add the v1 sample configuration (RSI mean-reversion) under `src/backtest/HTB.Backtest.Runner/samples/btc-rsi-mean-reversion.json`

**Checkpoint**: US1 is independently runnable end-to-end with an inline configuration (ideal fills, pass-through risk).

---

## Phase 4: User Story 2 - Risk limits enforced via the central risk layer (Priority: P2)

**Goal**: Route every simulated order through the authoritative central risk layer (max position
size, max daily loss, kill-switch) and simulate configured stop-loss/take-profit; record risk events.

**Independent Test**: Configure a low max position size / daily-loss / kill-switch drawdown and a
strategy that tries to exceed them; confirm orders are rejected or resized, halts occur, SL/TP
exits fire (stop-loss wins ties), and every action is recorded in the report.

### Tests for User Story 2 (write first, must fail) ⚠️

- [ ] T035 [P] [US2] Test `CompositeRiskPolicy`: position-size reject/resize per `OnBreach`, daily-loss halt for the day, kill-switch halt for the run; config risk advisory while central layer authoritative in `tests/shared/HTB.Shared.Tests/Risk/CompositeRiskPolicyTests.cs`
- [ ] T036 [P] [US2] Test `StopTakeProfitEvaluator` exit detection + precedence (stop-loss wins within a candle); exit reason set in `tests/backtest/HTB.Backtest.Tests/Execution/StopTakeProfitTests.cs`
- [ ] T037 [P] [US2] Test the engine records `RiskEvent`s (PositionSizeBreach, DailyLossHalt, KillSwitch) and applies the central layer as the gate in `tests/backtest/HTB.Backtest.Tests/Engine/RiskEnforcementTests.cs`

### Implementation for User Story 2

- [ ] T038 [US2] Implement `CompositeRiskPolicy` (position size, daily loss, kill-switch; veto/resize; fail closed) in `src/shared/HTB.Shared/Risk/CompositeRiskPolicy.cs` (makes T035 pass)
- [ ] T039 [US2] Implement `StopTakeProfitEvaluator` exit evaluation in `src/backtest/HTB.Backtest/Execution/StopTakeProfitEvaluator.cs` (makes T036 pass)
- [ ] T040 [US2] Integrate `CompositeRiskPolicy` as the authoritative gate in `BacktestEngine` (replace pass-through; map configuration requested-risk → effective `RiskLimits`; record `RiskEvent`s; apply SL/TP exits) in `src/backtest/HTB.Backtest/Engine/BacktestEngine.cs` (makes T037 pass)
- [ ] T041 [US2] Surface risk parameters (limits + SL/TP) through the strategy configuration and CLI in `src/backtest/HTB.Backtest.Runner/Program.cs`

**Checkpoint**: US1 + US2 work; risk enforcement and SL/TP are simulated and recorded.

---

## Phase 5: User Story 3 - Realistic cost modeling (fees & slippage) (Priority: P3)

**Goal**: Apply configurable fees and slippage to simulated fills; default zero.

**Independent Test**: Run the same configuration/range with zero then non-zero fees/slippage and
confirm the net result is lower by the modeled costs; zero costs match the US1 baseline.

### Tests for User Story 3 (write first, must fail) ⚠️

- [ ] T042 [P] [US3] Test fee + slippage application to fills (buys fill higher, sells lower; fee on traded value; zero ⇒ ideal baseline) in `tests/backtest/HTB.Backtest.Tests/Execution/FeeSlippageTests.cs`

### Implementation for User Story 3

- [ ] T043 [US3] Extend `NextBarOpenFillSimulator` with `FeeModel` + `SlippageModel` in `src/backtest/HTB.Backtest/Execution/NextBarOpenFillSimulator.cs` (makes T042 pass)
- [ ] T044 [US3] Surface fee/slippage settings through the run configuration and CLI in `src/backtest/HTB.Backtest.Runner/Program.cs`

**Checkpoint**: US1–US3 work; results reflect realistic execution costs.

---

## Phase 6: User Story 4 - Reproducible, configurable runs + result history (Priority: P4)

**Goal**: Persist configurations (versioned, immutable) and append-only result records in the
Strategy bounded context; reference configs by id+version or inline (inline registered as a
version); guarantee deterministic reproducibility.

**Independent Test**: Store a configuration, run it twice, confirm byte-identical numeric results
and two appended result records listable via the configuration version; confirm stored-ref ≡ inline.

### Tests for User Story 4 (write first, must fail) ⚠️

- [ ] T045 [P] [US4] Test `StrategyDbContext` model mapping (schema `strategy`, Guid keys, immutable versions, append-only result records, no cross-context FKs) in `tests/strategy/HTB.Strategy.Migrations.Tests/StrategyDbContextTests.cs`
- [ ] T046 [P] [US4] Integration-test `StrategyConfigurationRepository`/`BacktestResultRepository` (add version, get by id+version, **list all configurations** (FR-003e top-level), list versions per configuration, append result, list-for-version, and the **full Strategy → Version → Results traversal** (FR-018)) with Testcontainers in `tests/shared/HTB.Shared.Tests/Strategy/StrategyStoreIntegrationTests.cs`
- [ ] T047 [P] [US4] Test inline-config registration as a version + unknown-reference rejection (fail closed) in `tests/backtest/HTB.Backtest.Tests/Engine/ConfigResolutionTests.cs`
- [ ] T048 [P] [US4] Test determinism: identical config + data ⇒ identical numeric results (run timestamp excluded) in `tests/backtest/HTB.Backtest.Tests/Engine/DeterminismTests.cs`

### Implementation for User Story 4

- [ ] T049 [US4] Implement `StrategyDbContext` (schema `strategy`; `StrategyConfiguration`/`ConfigurationVersion`/`BacktestResultRecord` mappings, JSONB params, Guid keys) in `src/shared/HTB.Shared/Strategy/Persistence/StrategyDbContext.cs` (makes T045 pass)
- [ ] T050 [US4] Implement `IStrategyConfigurationRepository` (incl. a top-level **list-configurations** method per FR-003e) + `IBacktestResultRepository` and their EF implementations in `src/shared/HTB.Shared/Strategy/Persistence/` (makes T046 pass)
- [ ] T051 [US4] Implement `StrategyDbContextFactory` (env `HTB_STRATEGY_DB`) + initial migration in `src/strategy/HTB.Strategy.Migrations/` (mirrors MarketData)
- [ ] T052 [US4] Add configuration resolution (by id+version or inline→register version) to the engine/host (makes T047 pass) in `src/backtest/HTB.Backtest/Engine/BacktestEngine.cs`
- [ ] T053 [US4] Persist a `BacktestResultRecord` (full metric set + run config + timestamp) after each completed run, linked to the configuration version, in `src/backtest/HTB.Backtest/Engine/BacktestEngine.cs` (supports T048/SC-010)
- [ ] T054 [US4] Add CLI options for stored-config reference (`--config-id/--config-version`) and listing versions/results in `src/backtest/HTB.Backtest.Runner/Program.cs`

**Checkpoint**: All user stories complete; configurations and results are persisted, versioned, and reproducible.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T055 [P] Add structured logging + run metrics (candles processed, trades, rejections, duration, final metrics; no secrets) across engine/host (FR-014)
- [ ] T056 [P] Verify 100% line + branch coverage of `src/` via coverlet; add tests for any uncovered branches
- [ ] T057 [P] Update `docs/architecture/` with a backtest-runner + strategy-context page
- [ ] T058 Run `quickstart.md` validation scenarios 1–18 end-to-end and fix gaps
- [ ] T059 [P] Confirm no `float`/`double` in financial paths (constitution VII review)
- [ ] T060 [P] Add a large-range throughput/bounded-memory smoke test (~525,600 1-minute candles for one instrument completes in a single run within a soft few-minute budget) in `tests/backtest/HTB.Backtest.Tests/Engine/LargeRangePerformanceTests.cs` (validates SC-005)
- [ ] T061 [P] Add a no-secret-leak test asserting credentials/secrets never appear in run logs or the serialized report (constitution I) in `tests/backtest/HTB.Backtest.Tests/Observability/NoSecretLeakTests.cs` (addresses FR-014 observability with the security gate)

---

## Dependencies & Execution Order

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; BLOCKS all user stories.
- **US1 (Phase 3)**: depends on Foundational. MVP.
- **US2 (Phase 4)**: depends on US1 (replaces pass-through risk with the central layer; adds SL/TP).
- **US3 (Phase 5)**: depends on US1 (extends the fill simulator); independent of US2.
- **US4 (Phase 6)**: depends on US1 (persists its report/config); independent of US2/US3.
- **Polish (Phase 7)**: depends on all targeted stories.

### Within each story

- Tests are written first and must fail before implementation (TDD).
- Domain/abstractions before services; services before engine wiring; engine before CLI.

## Parallel Opportunities

- Setup: T003, T004, T005 in parallel (after T001/T002 csproj exist); T007 parallel.
- Foundational: T008, T009, T010, T011, T012 in parallel (distinct files); T013, T015, T017, T019 parallel.
- US1 tests T020–T024 in parallel; impl T027, T028, T029, T034 in parallel.
- US2 tests T035–T037 in parallel. US4 tests T045–T048 in parallel.

## Parallel Example: User Story 1 tests

```bash
Task: "Test StrategyFactory in tests/shared/HTB.Shared.Tests/Strategy/StrategyFactoryTests.cs"
Task: "Test RSI mean-reversion type in tests/shared/HTB.Shared.Tests/Strategy/RsiMeanReversionStrategyTests.cs"
Task: "Test Portfolio in tests/backtest/HTB.Backtest.Tests/Execution/PortfolioTests.cs"
Task: "Test PerformanceMetrics (9 metrics) in tests/backtest/HTB.Backtest.Tests/Reporting/PerformanceMetricsTests.cs"
Task: "Test BacktestEngine in tests/backtest/HTB.Backtest.Tests/Engine/BacktestEngineTests.cs"
```

## Implementation Strategy

- **MVP** = Phase 1 + Phase 2 + Phase 3 (US1): a runnable backtest from an inline configuration
  producing a full-metric report. Stop and validate here.
- **Incremental**: add US2 (central risk enforcement + SL/TP) → US3 (costs) → US4 (persistence/
  versioning/reproducibility), validating each independently.
- Per repo rule, every `src/` line must reach 100% line + branch coverage before shipping.

## Notes

- A strategy *type* is code (shipped); a strategy *configuration* is data (stored, versioned, Guid id).
- The central risk layer is authoritative; the configuration's risk block is advisory input.
- All money/quantity/ratio values are `decimal`; the only wall-clock value is the persisted result
  timestamp (metadata, excluded from numeric results — SC-002).
- Live-only fields in the stored model (status lifecycle, runtime state, live trade audit, order
  retry/idempotency/TIF, schedule) are carried but NOT implemented here (out of scope, FR-003h).
