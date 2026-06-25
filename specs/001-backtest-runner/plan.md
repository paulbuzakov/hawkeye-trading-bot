# Implementation Plan: Backtest Runner

**Branch**: `001-backtest-runner` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-backtest-runner/spec.md`

## Summary

Build a deterministic backtest runner that replays stored historical OHLCV candles through a
**code-defined strategy type** (a deterministic signal-generating algorithm that ships with the
bot) parameterized by a **stored, versioned configuration**, simulates the resulting orders
against a starting capital, applies configurable fees and slippage, and produces a reproducible
performance report. Every simulated order passes through the **shared central risk layer** — the
final authority (max position size, max daily loss, kill-switch) — and the configuration's
stop-loss/take-profit are simulated. After each run, a **result summary is persisted and linked to
the exact configuration version** that produced it.

Strategies are split into **code** and **data**: the algorithm (strategy *type*) is code; only the
*configuration* (parameter values, instrument binding, requested risk, execution settings) is
stored. A **Strategy Factory** validates a configuration's parameters against its type's parameter
spec and instantiates the type — there is no JSON interpreter. Strategy types, configurations,
their immutable **versions**, and **result records** live in a dedicated **Strategy bounded
context** inside `HTB.Shared` with its own database context (schema `strategy`, own migrations
assembly), isolated from the market-data context. A run references a configuration by id + version
or supplies an inline configuration (registered as a version so its result is never orphaned).

The design follows the constitution's layered separation: independent **data**, **strategy** (types
+ factory + config store), **risk** (central, authoritative), and **execution** layers, wired by a
thin orchestration engine and a CLI host, all via DI, with money math in `decimal`.

## Technical Context

**Language/Version**: C# 13 on .NET 10

**Primary Dependencies**: `HTB.Shared` (existing — `ICandleRepository`, `Candle`, `Symbol`,
`Timeframe`; new — Risk and Strategy contexts); EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL`
10.0.2 (Strategy store, mirroring market-data persistence); `System.Text.Json` (configuration
parse/serialize); `Microsoft.Extensions.DependencyInjection`, `.Logging`, `.Configuration` (CLI
host). No new exchange/network dependencies.

**Storage**:
- **Read-only** historical candles from the existing market-data store via `ICandleRepository`.
- **New** Strategy store (PostgreSQL, schema `strategy`): strategy configurations, immutable
  configuration versions, and backtest result records. Own `StrategyDbContext` in `HTB.Shared`;
  migrations in a new `HTB.Strategy.Migrations` project (mirrors `HTB.MarketData.Migrations`).
  Connection from env (`HTB_STRATEGY_DB`).

**Testing**: xUnit + coverlet. Unit tests use in-memory fakes (`ICandleRepository`, configuration/
result repositories) — no DB. Integration tests for the Strategy store + migrations use
Testcontainers.PostgreSql, consistent with existing `HTB.Shared.Tests`. No live exchange calls.

**Target Platform**: Cross-platform .NET 10 console application (Linux/macOS).

**Project Type**: Single solution area — engine library + CLI host + Strategy migrations project +
test projects. Risk and Strategy contexts live in `HTB.Shared` for reuse by future live execution.

**Performance Goals**: Process a full year of 1-minute candles for one instrument (~525,600
candles, SC-005) in a single run; forward-pass replay with bounded memory and incremental metrics.

**Constraints**: Deterministic numeric results for identical config + data (SC-002); all monetary
and quantity math in `decimal`; no look-ahead (strategy types/indicators see only candles at or
before the current simulated time); no live orders or connectivity (FR-015); stored configuration
versions and result records are immutable/append-only (auditability). Identity for stored
aggregates is `Guid` (stable, append-only-audit friendly).

**Scale/Scope**: v1 = single instrument + single timeframe per run; ships at least one strategy
type (RSI mean-reversion) plus a sample configuration. In scope: strategy type + parameters +
instrument binding + requested risk (incl. **stop-loss/take-profit**) + modeled fees/slippage +
period/capital, and the **full metric set** (total return, annualized return, Sharpe, Sortino, max
drawdown, win rate, profit factor, total trades, avg trade duration). Out of scope (carried in the
stored model but not implemented here): paper/live status lifecycle, runtime state, live
trade-history audit, order retry/idempotency/post-only/time-in-force, schedule; plus
multi-instrument/portfolio, multi-timeframe, trailing stops, and a config management API/UI.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|-----------|------|--------|
| I. Security First | No hardcoded/logged secrets; DB connection strings from env (`HTB_STRATEGY_DB`, market-data equivalent); reports/logs carry no credentials. | PASS |
| II. Capital Safety | Every order path enforces risk limits; non-live by default. | PASS — the only order path is the simulated one, routed through the authoritative central risk layer (FR-005); config risk is advisory; runner is inherently non-live (FR-015). |
| III. Reliability & Fail-Closed | Timeouts on I/O; fail closed. | PASS — DB reads/writes honor `CancellationToken`; invalid config, out-of-spec parameters, unknown type, unknown configuration reference, and missing data all fail closed before/at run start (FR-003a/f, FR-012, FR-013). |
| IV. Test-First | TDD; edge cases; mocked clients. | PASS — types, factory, engine, risk, execution, and stores built test-first with fakes; Testcontainers for store integration; no live calls. |
| V. Layered Architecture & DI | Strategy/execution/risk/data independent; DI; async I/O. | PASS — four layers + orchestration; Strategy and Risk are independent contexts in `HTB.Shared`; data/store access async; DI in the host. |
| VI. Observability & Auditability | Structured logs/metrics; auditable state changes. | PASS — per-run logs/metrics (FR-014); immutable configuration versions + append-only result records make strategy and outcome history auditable (FR-003e, FR-016); full config echoed in report (SC-007). |
| VII. Deterministic Arithmetic | `decimal` only for money. | PASS — all balances/prices/sizes/indicators/P&L in `decimal` (FR-009, FR-003c); deterministic replay (FR-010); the persisted run timestamp is metadata only (FR-017). |

**Result**: All gates pass. No deviations to record in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-backtest-runner/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── README.md
│   ├── strategy-type.md      # code strategy-type + parameter-spec contract
│   ├── strategy-factory.md   # validate params vs spec + instantiate type
│   ├── strategy-store.md     # configuration/version/result persistence
│   ├── risk.md               # central risk layer (authoritative)
│   ├── execution.md          # fill simulator (fees/slippage) + SL/TP
│   ├── data-source.md        # candle data source adapter
│   └── report.md             # report + result-record schema (full metrics)
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
├── shared/
│   └── HTB.Shared/
│       ├── Risk/                              # NEW: central risk layer (shared with live)
│       │   ├── Abstractions/IRiskPolicy.cs
│       │   ├── Domain/{RiskLimits,OrderIntent,RiskDecision,RiskState}.cs
│       │   ├── PassThroughRiskPolicy.cs       # approve-all (US1 order path)
│       │   └── CompositeRiskPolicy.cs         # authoritative: position/daily-loss/kill-switch
│       └── Strategy/                          # NEW: strategy bounded context (shared with live)
│           ├── Abstractions/
│           │   ├── IStrategy.cs               # executable strategy (factory output)
│           │   ├── IStrategyContext.cs
│           │   ├── IStrategyType.cs           # code algorithm + ParameterSpec
│           │   ├── IStrategyConfigurationRepository.cs
│           │   └── IBacktestResultRepository.cs
│           ├── Domain/
│           │   ├── StrategyConfiguration.cs   # config aggregate (Guid id, name)
│           │   ├── ConfigurationVersion.cs    # immutable version (config id + version + JSONB)
│           │   ├── InstrumentBinding.cs
│           │   ├── BacktestResultRecord.cs    # summary + run config + timestamp
│           │   ├── ParameterSpec.cs
│           │   ├── Indicators/                 # SMA/EMA/RSI (decimal, no look-ahead)
│           │   └── Types/RsiMeanReversionStrategy.cs   # the shipped v1 strategy type
│           ├── StrategyFactory.cs             # validate params vs spec → instantiate type
│           └── Persistence/
│               ├── StrategyDbContext.cs       # schema "strategy"
│               ├── StrategyConfigurationRepository.cs
│               └── BacktestResultRepository.cs
└── backtest/
    ├── HTB.Backtest/                          # NEW: engine library
    │   ├── Configuration/{BacktestConfiguration,BacktestConfigurationValidator}.cs
    │   ├── Data/{IBacktestDataSource,RepositoryBacktestDataSource}.cs
    │   ├── Execution/{IFillSimulator,NextBarOpenFillSimulator,StopTakeProfitEvaluator,Portfolio}.cs
    │   ├── Reporting/{BacktestReport,PerformanceMetrics}.cs
    │   └── Engine/BacktestEngine.cs           # replay loop + risk gate + result persistence
    └── HTB.Backtest.Runner/                   # NEW: CLI host (DI, config load, run)
        └── Program.cs

src/strategy/
└── HTB.Strategy.Migrations/                   # NEW: EF migrations for StrategyDbContext
    └── StrategyDbContextFactory.cs            # env var HTB_STRATEGY_DB (mirrors marketdata)

tests/
├── shared/HTB.Shared.Tests/                   # extend: Risk + Strategy (types/factory/store) tests
├── backtest/HTB.Backtest.Tests/              # NEW: engine/execution/config tests (fakes)
└── strategy/HTB.Strategy.Migrations.Tests/   # NEW: migration/DbContext tests (Testcontainers)
```

**Structure Decision**: Matches the existing `HTB.<Area>` convention. Risk and the Strategy bounded
context live in `HTB.Shared` so the identical risk policy and strategy types/configurations govern
both backtest and future live execution. The Strategy store mirrors the market-data persistence
pattern exactly: a `StrategyDbContext` in `HTB.Shared` (schema `strategy`) plus a separate
`HTB.Strategy.Migrations` project with a design-time factory reading `HTB_STRATEGY_DB`. All new
projects register in `HTB.slnx`.

## Complexity Tracking

> No constitution violations. No entries required.
>
> Note: the Strategy store adds a second bounded context/DbContext. This is an explicit spec
> requirement (FR-003d), not incidental complexity — it mirrors the existing market-data context
> and keeps strategy definitions/outcomes isolated and reusable by future live execution.
