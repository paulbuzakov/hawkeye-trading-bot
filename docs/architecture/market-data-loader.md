# Market Data Loader — Binance Backfill

`HTB.MarketData.Loader` is the console app that backfills OHLCV candles from
**Binance's public REST API** into the candle store described in
[market-data-storage.md](market-data-storage.md). It is driven by a `symbols.json`
manifest and is safe to re-run — every write is an idempotent upsert on the candle's
natural key.

## Manifest (`symbols.json`)

A JSON array of entries. `//` / `/* */` comments and trailing commas are tolerated.

```json
[
  {
    "ticket": "BTCUSDT",
    "timeframes": ["M1", "M5"],
    "date-range": {
      "from": "2020-01-01",
      "to": "2026-12-31"
    }
  }
]
```

| Field          | Required | Notes                                                            |
| -------------- | -------- | --------------------------------------------------------------- |
| `ticket`       | yes      | Binance symbol, e.g. `BTCUSDT`.                                  |
| `timeframes`   | yes      | One or more `Timeframe` names: `M1 M5 M15 H1 H4 D1` (any case).  |
| `date-range.from` | yes   | Inclusive lower bound (UTC). Dates are parsed as UTC midnight.   |
| `date-range.to`   | no    | Inclusive upper bound. Omitted ⇒ "up to now".                   |

Validation failures (missing ticker, empty/unknown timeframe, bad or inverted date
range) raise `SymbolConfigException` with a message naming the offending entry.

## Pipeline

For each entry the loader:

1. Resolves the `binance` exchange and the symbol row via `IInstrumentRepository`
   (`GetOrCreate…`), looking the asset breakdown up from Binance `exchangeInfo`.
2. Picks the start point per timeframe: the open time of the last stored candle
   (`ICandleRepository.GetLatestAsync`) if any, otherwise the manifest's `from`. Restarting
   the loader therefore tops up from where it left off instead of re-downloading the range.
3. Streams klines per timeframe from `/api/v3/klines` one page at a time, paginating over
   Binance's 1000-bar request cap (`BinanceMarketDataClient`).
4. Maps each page's klines to `Candle`s, **skipping the still-forming bar** (one whose close
   time hasn't passed) so only final bars are stored, and upserts each page via
   `ICandleRepository` as it arrives, so a long backfill is persisted incrementally rather than
   buffered in memory.
5. Logs progress after every page (`[ nn%] TICKER TF: n candles`): a time-based estimate of
   how much of the remaining window is covered, snapping to `100%` on the final page.

```
symbols.json → SymbolConfigParser → MarketDataLoader
                                       ├─ IBinanceMarketDataClient (exchangeInfo + klines)
                                       ├─ IInstrumentRepository    (exchange/symbol FKs)
                                       └─ ICandleRepository        (idempotent upsert)
```

## Configuration

| Setting             | Source                         | Default                          |
| ------------------- | ------------------------------ | -------------------------------- |
| Manifest path       | `args[0]` or `HTB_SYMBOLS_FILE`| `symbols.json` (next to the app) |
| DB connection       | `HTB_MARKETDATA_DB`            | local `htb_marketdata` (see factory) |

Run locally:

```bash
dotnet run --project src/marketdata/HTB.MarketData.Loader -- path/to/symbols.json
```

In the Docker stack the loader runs after migrations complete; it reads
`HTB_MARKETDATA_DB` from `deploy/docker-compose.yml`.
