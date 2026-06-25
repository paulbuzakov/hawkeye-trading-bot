# Contract: Backtest Data Source

**Namespace**: `HTB.Backtest.Data` · **Satisfies**: FR-001, FR-002, FR-013 · **Ref**: R1, R11

Adapts the existing `ICandleRepository` into an ordered candle feed for replay, decoupling the
engine from EF Core/persistence and making it trivially fakeable in unit tests.

```csharp
public interface IBacktestDataSource
{
    // Candles for the instrument/timeframe in [from, to], ascending open time, closed candles only.
    Task<IReadOnlyList<Candle>> LoadAsync(
        int symbolId, Timeframe interval, DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
```

Default implementation `RepositoryBacktestDataSource` delegates to
`ICandleRepository.GetRangeAsync` (already returns ascending-ordered candles).

**Rules**
- MUST preserve ascending open-time order (replay correctness, FR-002).
- MUST honor `CancellationToken`/timeout (fail-closed I/O, constitution III).
- Empty result ⇒ engine produces a `NoData` outcome, not a misleading empty success (FR-013).
- Gaps (missing candles) are detected by the engine from open-time spacing and reported in
  `DataGaps`; prices are never fabricated (FR-013).
- Interface shaped so a future streaming/paged implementation can replace it without engine
  changes (R11), supporting the large-range goal (SC-005).
