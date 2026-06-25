# Contract: Backtest Report (output schema) + Result Record

**Namespace**: `HTB.Backtest.Reporting` · **Satisfies**: FR-007, FR-008, FR-011, FR-016/017,
SC-007/010/011

The engine returns a `BacktestReport` object; the CLI host serializes it to JSON and prints a
human-readable summary; the engine also persists a **summary** of it as a `BacktestResultRecord`
linked to the resolved configuration version (see [strategy-store.md](./strategy-store.md)).

**Report JSON shape** (illustrative):

```json
{
  "outcome": "Completed",
  "resolvedConfigurationVersion": { "configurationId": "…", "versionNumber": 3 },
  "configuration": {
    "instrument": "BTC/USDT", "timeframe": "1h",
    "from": "2023-01-01T00:00:00Z", "to": "2025-12-31T23:59:59Z",
    "startingCapital": "10000",
    "strategy": {
      "typeName": "RsiMeanReversion",
      "parameters": { "rsiPeriod": "14", "oversoldThreshold": "30", "overboughtThreshold": "65", "trendFilterPeriod": "200" },
      "requestedRisk": { "maxPositionSizeQuote": "500", "stopLossPct": "0.03", "takeProfitPct": "0.06", "maxDailyLossQuote": "200", "killSwitchDrawdownPct": "0.15" }
    },
    "fees": { "rate": "0.001" }, "slippage": { "amount": "0.0005" }
  },
  "startingBalance": "10000",
  "endingBalance": "11840.00",
  "metrics": {
    "totalReturn": "0.184", "annualizedReturn": "0.058",
    "sharpeRatio": "1.12", "sortinoRatio": "1.47",
    "maxDrawdown": "0.119", "winRate": "0.523",
    "profitFactor": "1.34", "totalTrades": 214, "avgTradeDurationHours": "19.5"
  },
  "trades": [ /* SimulatedTrade[] incl. exitReason ∈ {Signal,StopLoss,TakeProfit,EndOfRange} */ ],
  "riskEvents": [ { "time": "…", "type": "PositionSizeBreach", "detail": "order 2.0 > max 1.0; resized to 1.0" } ],
  "dataGaps": [ { "from": "…", "to": "…", "missingBars": 3 } ],
  "candlesProcessed": 26280
}
```

**Persisted result record** (subset, append-only): `configurationId`+`versionNumber`, instrument,
timeframe, from/to, startingCapital, fees/slippage, the **full metrics block**, and a
`runTimestamp`. The full `trades`/`riskEvents` arrays are **not** stored in the record (kept as a
separate run artifact, FR-017).

**Rules**
- All money/quantity/ratio values serialized from `decimal` as strings (precision).
- `configuration` echoes the fully resolved strategy configuration so a run is reproducible from
  the report alone (SC-007); `resolvedConfigurationVersion` ties it to the persisted record.
- Metrics include the **full set** (total return, annualized return, Sharpe, Sortino, max drawdown,
  win rate, profit factor, total trades, avg trade duration — FR-007, SC-011).
- `outcome` ∈ {`Completed`, `NoData`, `ValidationFailed`}; non-`Completed` carries a message and
  empty trade/metric sets, and persists no result record.
- Trades and risk events are chronological. `runTimestamp` is metadata, excluded from the
  deterministic numeric results (SC-002).
