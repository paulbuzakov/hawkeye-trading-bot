# Contract: Strategy Store (configuration / version / result persistence)

**Namespace**: `HTB.Shared.Strategy.Persistence` · **Satisfies**: FR-003d/e/f, FR-016/017/018,
SC-009/010 · **Ref**: R5, R6, R7

A dedicated bounded context with its own `StrategyDbContext` (PostgreSQL schema `strategy`) and a
separate `HTB.Strategy.Migrations` project (design-time factory reads `HTB_STRATEGY_DB`), mirroring
the market-data persistence pattern. Isolated from the market-data context. Stored ids are `Guid`.

```csharp
public interface IStrategyConfigurationRepository
{
    // Persist a new immutable version of a configuration (new configuration if name is new).
    Task<ConfigurationVersionRef> AddVersionAsync(StrategyConfigurationDraft draft, CancellationToken ct = default);

    // Retrieve a specific immutable version; null if (id, version) absent.
    Task<ConfigurationVersion?> GetVersionAsync(ConfigurationVersionRef reference, CancellationToken ct = default);

    // List configurations and their version history.
    Task<IReadOnlyList<StrategyConfiguration>> ListAsync(CancellationToken ct = default);
}

public interface IBacktestResultRepository
{
    // Append a result record linked to a configuration version (never overwrites).
    Task AddAsync(BacktestResultRecord record, CancellationToken ct = default);

    // List a version's result history, newest first.
    Task<IReadOnlyList<BacktestResultRecord>> ListForVersionAsync(ConfigurationVersionRef reference, CancellationToken ct = default);
}
```

**Rules**
- Configuration **versions are immutable**; a change adds a new version (FR-003e). `(configurationId,
  versionNumber)` is unique and monotonic.
- An **inline** configuration supplied to a run is persisted via `AddVersionAsync` **before** the
  run so its result links to a concrete version — no orphaned results (FR-003f, R6).
- A reference to a missing `(id, version)` ⇒ resolution fails closed before any data read.
- Result records are **append-only**; a version may have many (FR-016, SC-010).
- All I/O is async with `CancellationToken`; connection string from env (`HTB_STRATEGY_DB`),
  never hardcoded/logged (constitution I).
- No shared tables or FKs with the market-data schema (FR-003d isolation).
- Strategy *types* are code and are NOT stored here — a configuration references its type by name.
