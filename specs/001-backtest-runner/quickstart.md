# Quickstart & Validation: Backtest Runner

Proves the runner works end-to-end and validates the spec's success criteria. For the strategy-type
and configuration shapes, layer contracts, and entities see [contracts/](./contracts/) and
[data-model.md](./data-model.md).

## Prerequisites

- .NET 10 SDK
- **Market-data store** reachable and populated for at least one instrument/timeframe/range (use the
  existing `HTB.MarketData.Loader`, e.g. `BTC/USDT` 1h). Connection via env, never hardcoded.
- **Strategy store** reachable; apply migrations for the `strategy` schema:
  ```bash
  export HTB_STRATEGY_DB="Host=localhost;Port=5432;Database=htb_strategy;Username=postgres;Password=postgres"
  dotnet ef database update --project src/strategy/HTB.Strategy.Migrations
  ```
- Solution builds: `dotnet build HTB.slnx`

## Build & test

```bash
dotnet test HTB.slnx     # unit tests use fakes; store integration uses Testcontainers
```

Expected: all tests pass at 100% line + branch coverage of `src/` (repo rule).

## Run a backtest

**With an inline configuration** (registered as a version automatically):

```bash
dotnet run --project src/backtest/HTB.Backtest.Runner -- \
  --instrument BTC/USDT --timeframe 1h \
  --from 2023-01-01T00:00:00Z --to 2025-12-31T23:59:59Z \
  --capital 10000 --config-file ./samples/btc-rsi-mean-reversion.json \
  --fee 0.001 --slippage 0.0005
```

The configuration names a **code strategy type** (e.g. `RsiMeanReversion`) and supplies its
parameters, instrument binding, and requested risk (stop-loss/take-profit, max position, etc.).

**With a stored configuration** (by id + version):

```bash
dotnet run --project src/backtest/HTB.Backtest.Runner -- \
  --instrument BTC/USDT --timeframe 1h --from … --to … --capital 10000 \
  --config-id <guid> --config-version 3
```

Expected: a `BacktestReport` JSON (see [contracts/report.md](./contracts/report.md)) with
`outcome: "Completed"`, trades (with exit reasons), the full metric set, and a
`resolvedConfigurationVersion`; plus a printed summary. A result record is persisted, linked to
that version.

## Validation scenarios (map to Success Criteria & user stories)

| # | Scenario | Expected | Validates |
|---|----------|----------|-----------|
| 1 | Core run from a configuration | Report with trades, ending balance, full metrics | US1, SC-001 |
| 2 | No-signal configuration | 0 trades, ending balance = starting capital | US1 #2 |
| 3 | Determinism | Identical config + data twice ⇒ byte-identical numeric results | US4, SC-002 |
| 4 | Position-size limit | Offending order rejected/resized by central risk layer; `RiskEvent` recorded | US2, SC-003 |
| 5 | Daily-loss halt | Entries halt for that day; event recorded | US2 |
| 6 | Kill-switch | All trading halts after trip; event recorded | US2 |
| 7 | Stop-loss / take-profit | Position closed at configured level; exit reason recorded (stop-loss wins ties) | US2 |
| 8 | Fees/slippage | Non-zero costs net lower than zero-cost baseline | US3, SC-004 |
| 9 | Large range | A multi-year 1h (or 1y 1m) run completes in one run | SC-005 |
| 10 | Invalid config | `outcome: ValidationFailed` before any data read | SC-006 |
| 11 | Unknown type / out-of-spec parameter | Rejected before data read; message identifies the fault | FR-003a |
| 12 | Unknown configuration reference | "configuration not found" before data read | Edge case |
| 13 | No data | `outcome: NoData`, not empty "success"; no result persisted | Edge case |
| 14 | Indicator warm-up | No trades fire before required indicators have enough candles | FR-003c |
| 15 | Add strategy instance via config only | New configuration backtested with no code change | SC-008 |
| 16 | Store/version/list | Configuration stored, versioned, retrieved by id+version; stored ≡ inline results | SC-009 |
| 17 | Result history | A version's runs accumulate as append-only records, listable per version | SC-010 |
| 18 | Full metric set | Report/result include all 9 metrics | SC-011 |

## Notes

- The runner never contacts a live exchange and places no real orders (FR-015).
- Strategy *types* are code (shipped); strategy *configurations* are data (stored, versioned).
- The central risk layer is authoritative; the configuration's risk block is advisory input.
- Live-only fields in the stored model (status lifecycle, runtime state, live trade audit, order
  retry/idempotency/TIF, schedule) are carried but NOT implemented here (out of scope, FR-003h).
- All money/quantity/ratio values are `decimal`; the only wall-clock value is the persisted result
  timestamp (metadata, excluded from numeric results — SC-002).
