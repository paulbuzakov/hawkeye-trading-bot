# HTB .NET conventions & idioms

These are extracted from the live codebase. New code should be indistinguishable from what's here. When in doubt, copy the shape of `HTB.Shared` + `HTB.MarketData.Loader`.

## Baseline

- **`net10.0`**, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` in every `csproj`.
- Common namespaces imported once via `<Using Include="…" />` in the `csproj` (e.g. `Microsoft.EntityFrameworkCore`), not repeated `using` lines.
- EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10 over PostgreSQL/TimescaleDB.
- Tests: xUnit + `Testcontainers.PostgreSQL` + `coverlet.collector`. `<Using Include="Xunit" />` global.

## Project & folder layout

```
src/<area>/HTB.<Domain>.<Component>/        e.g. src/marketdata/HTB.MarketData.Loader
src/shared/HTB.Shared/                       cross-cutting domain + read contracts
tests/<area>/HTB.<Domain>.<Component>.Tests/ mirrors source 1:1
```

- Folders inside a project are by **feature/role**: `Domain/`, `Abstractions/`, `Persistence/`, plus component folders like `Binance/`, `Configuration/`, `Ingestion/`.
- Project naming: `HTB.<Domain>.<Component>` (e.g. `HTB.MarketData.Loader`, `HTB.MarketData.Migrations`). Solution is `HTB.slnx` with `/src/<area>/` and `/tests/<area>/` solution folders.
- One `.csproj` per deployable/testable unit. Migrations are their own project.

## Where a type lives — the read/write split

This is the defining architectural decision of the repo. Honor it.

- **`HTB.Shared` holds the domain model + read-only contracts + read implementations.** Anything that *queries* (backtest, analytics, resume logic) depends only on Shared. Example: `ICandleRepository` (interface in `MarketData/Abstractions/`) + `CandleRepository` (impl in `MarketData/Persistence/`) + `MarketDataReadonlyDbContext` (no-tracking, also the EF migration target).
- **Write concerns live in the owning service, not Shared.** The loader owns `ICandleWriter`, `CandleWriter`, `IInstrumentRepository`, `InstrumentRepository`, and `MarketDataWriteDbContext` (tracked). Rationale stated in the code: "Writes are the loader's concern."
- **Shared mapping, split contexts:** `MarketDataDbContextBase` (abstract, primary-ctor, holds all `OnModelCreating` mapping) → `MarketDataReadonlyDbContext` (no-tracking, migration target) and `MarketDataWriteDbContext` (tracked). Both map the same tables; only one is migrated through.

When you add a layer, ask: *is this a read others need (→ Shared) or a write/behavior I own (→ my service)?*

## C# style

- **Sealed by default.** Domain entities, repos, contexts, DTOs are `sealed`.
- **Primary constructors** for DI: `public sealed class CandleRepository(MarketDataReadonlyDbContext db) : ICandleRepository` and assign to a `private readonly` field. Abstract bases too: `public abstract class MarketDataDbContextBase(DbContextOptions options) : DbContext(options)`.
- **Records for immutable config/DTOs:** `public sealed record SymbolLoadSpec(string Ticker, IReadOnlyList<Timeframe> Timeframes, DateTimeOffset From, DateTimeOffset? To);`.
- **Enums stored as stable numeric codes:** `enum Timeframe : short { M1 = 1, … H1 = 60, … }` mapped with `.HasConversion<short>()`. The on-disk value is independent of declaration order — never renumber.
- **XML doc comments on every public type and member**, explaining intent and invariants (not restating the signature). This is a hard house style — match the density you see in `Candle.cs`, `ICandleRepository.cs`, `MarketDataLoader.cs`.
- **`CancellationToken ct = default`** as the last parameter on every async method; flow it through (`.WithCancellation(ct)` on async streams).
- **Guard clauses:** `ArgumentNullException.ThrowIfNull(x)` at public entry points.
- **Custom exceptions** for domain errors (`SymbolConfigException`), not bare `Exception`.
- **`IReadOnlyList<T>` / `IReadOnlyCollection<T>`** at boundaries, not `List<T>`.
- `internal` + `InternalsVisibleTo("…​.Tests")` when tests need access without widening the public surface (see the Migrations project).

## EF Core / persistence idioms

- **snake_case** column & table names mapped explicitly (`.HasColumnName("open_time")`, `.ToTable("candles", "marketdata")`); a `marketdata` default schema.
- Natural/composite keys declared explicitly: `entity.HasKey(c => new { c.ExchangeId, c.SymbolId, c.Interval, c.OpenTime })`.
- DB-managed columns via `.HasDefaultValueSql("now()")` (e.g. `ingested_at`).
- FKs with `.OnDelete(DeleteBehavior.Restrict)` for reference integrity.
- **Idempotent writes** via raw `ExecuteSqlInterpolatedAsync` with `INSERT … ON CONFLICT (natural key) DO UPDATE` when EF's change tracker isn't the right tool (bulk upsert). Parameters are interpolated safely (FormattableString), never string-concatenated.
- **Get-or-create** for reference rows (exchange, symbol), idempotent on natural keys.
- Migrations target the **read-only** context; the write context maps the same tables but is never migrated through.

## Testability seams (these make 100% coverage cheap)

- **Inject `TimeProvider`** instead of calling `DateTimeOffset.UtcNow`. Production wires `TimeProvider.System`; tests use a fake to control "now" (e.g. to exercise the forming-bar cutoff).
- **Inject side effects as delegates** when an interface is overkill: `Action<string>? log = null` with a `?? (_ => {})` default.
- **Everything behind an interface** at I/O edges: `IBinanceMarketDataClient`, `ICandleWriter`, `IInstrumentRepository`. Cores depend on interfaces; only the composition root sees concretes.
- **Composition root is the only place `new`ing infra**, and it's `[ExcludeFromCodeCoverage]` (`Program.cs`): "Pure composition (real HTTP + PostgreSQL wiring), so it is excluded from coverage; the testable logic lives in the parser, client, and loader types." Apply `[ExcludeFromCodeCoverage]` only to genuinely-untestable composition, never to logic.
- **Integration tests** use `Testcontainers.PostgreSql` against a real Timescale image via a shared fixture/collection (`TimescaleDatabaseFixture`, `[Collection(nameof(TimescaleCollection))]`); the read repo is tested by seeding through the real write path.
- Test method naming: `Method_does_thing_under_condition` (snake-ish), `[Fact]` / `[Theory]`.

## Configuration & entry points

- Console apps read config from args-then-env (`HTB_SYMBOLS_FILE`, `HTB_CONNECTION_STRING`), throwing a clear `InvalidOperationException` when a required value is missing.
- `Main` returns `int` (0 ok / 1 failed), wraps the run in try/catch, writes errors to stderr.
- Deployment is Docker Compose: a Timescale service, a one-shot migrations container (`restart: "no"`, runs to completion), then the app (`depends_on … condition: service_completed_successfully`). Build context is the repo root so the csproj graph restores.

## Checklist when adding a component

- [ ] Reads others need → `HTB.Shared` (interface in `Abstractions/`, impl in `Persistence/`). Writes/behavior → owning service.
- [ ] Domain types `sealed`, `decimal` money, `DateTimeOffset` UTC, enums as fixed `short`/`int` codes.
- [ ] Primary-ctor DI, `CancellationToken` last, `IReadOnly*` at boundaries, XML docs on public API.
- [ ] `TimeProvider` + interfaces for every side effect; pure core separated from composition.
- [ ] Idempotent writes on a natural key.
- [ ] Test project mirrors path; pure logic → unit tests, DB → Testcontainers; composition root `[ExcludeFromCodeCoverage]`.
- [ ] 100% line + branch.
