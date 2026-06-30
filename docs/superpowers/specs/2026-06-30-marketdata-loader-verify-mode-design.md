# Marketdata-loader verify mode — design

## Problem

The market-data loader backfills Binance candles into TimescaleDB. To keep
restarts cheap it **resumes** from the last stored bar, so a normal run only
fetches *new* candles ([`MarketDataLoader.LoadSymbolAsync`][loader]):

```csharp
var latest = await _candleReader.GetLatestAsync(symbol.Code, timeframe, cancellationToken);
var from = latest?.OpenTime ?? spec.From;
```

That resume shortcut means the loader never re-examines candles it has already
stored. If a previously stored bar drifted from the exchange's final values, or
a gap exists inside the already-covered range, a normal run will never correct
it.

We want an opt-in **verify** pass that re-checks every candle from the start of
each symbol's configured range and fixes any mismatches.

## Goal

Add an `.env`-driven toggle that, when enabled, makes the loader re-scan each
symbol/timeframe from `spec.From` (ignoring the resume point) and upsert every
fetched bar, so stored candles that drifted are corrected and missing bars are
filled.

## Approach

Verify mode is **"a normal load minus the resume shortcut."** The loader already
writes through an idempotent `INSERT ... ON CONFLICT DO UPDATE` upsert
([`CandleWriter.UpsertAsync`][writer]), so:

- A bar whose stored values match the exchange is rewritten with identical
  values — a harmless no-op update.
- A bar whose stored values drifted is corrected.
- A missing bar inside the range is inserted.

Therefore verify mode needs no new comparison logic — only a change to where the
fetch starts.

### Behaviour change

`MarketDataLoader.LoadAsync` gains a `bool verify = false` parameter. The resume
line becomes:

```csharp
var from = verify
    ? spec.From
    : (latest?.OpenTime ?? spec.From);
```

When `verify` is `false` (the default) behaviour is unchanged. When `true`, the
fetch always starts at `spec.From` for every timeframe, re-streaming the full
range through the existing per-page idempotent upsert.

### Configuration

A new boolean environment variable `HTB_VERIFY` (default `false`):

- **`deploy/.env` / `deploy/.env.example`** — add `HTB_VERIFY=false` with a
  one-line comment explaining the re-scan-and-correct behaviour.
- **`deploy/docker-compose.yml`** — add `HTB_VERIFY: "${HTB_VERIFY:-false}"` to
  the `marketdata-loader` service's `environment` block. No separate service or
  profile; the toggle reuses the existing loader run.
- **`Program.cs`** — read `HTB_VERIFY`, parse it as a boolean, and pass it to
  `LoadAsync`. `Program` is `[ExcludeFromCodeCoverage]`, so the env read lives
  there; the testable behaviour is the `verify` branch in `LoadAsync`.

Boolean parsing: treat `true`/`1` (case-insensitive) as enabled, everything else
(including unset/empty) as disabled, so a stray value never silently triggers a
full re-scan.

## Out of scope (YAGNI)

- **Precise mismatch reporting.** Counting/logging only the bars that actually
  differed would need a `WHERE` diff clause on the upsert plus extra reads. The
  unconditional idempotent upsert already corrects mismatches; a "verify report"
  can be added later if needed.
- **A separate `marketdata-verify` compose service.** The requirement is an env
  toggle on the existing loader, not a distinct service.

## Testing

`HTB.MarketData.Loader` unit tests (100% coverage rule; `Program` excluded):

- **Verify re-scans from start.** Given a stored latest bar and `verify: true`,
  assert the client is asked to stream klines from `spec.From`, not from the
  latest bar's open time.
- **Normal mode still resumes.** Given a stored latest bar and `verify: false`
  (default), assert the stream starts at the latest bar's open time — pins the
  existing behaviour.

[loader]: ../../../src/marketdata/HTB.MarketData.Loader/Ingestion/MarketDataLoader.cs
[writer]: ../../../src/marketdata/HTB.MarketData.Loader/Persistence/CandleWriter.cs
