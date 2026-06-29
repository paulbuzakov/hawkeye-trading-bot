# Loader: parse and persist `rules.json` alongside `meta.json`

Date: 2026-06-30
Branch: `feat/strategy-rules-domain`
Status: Approved design — ready for implementation plan

## Goal

Extend the strategy loader CLI from

```
loader --meta <path>
```

to

```
loader --meta <path> --rules <path>
```

so a single invocation parses both a bundle's `meta.json` and its `rules.json` and
persists the `StrategyDefinition` **and** its `StrategyRuleSet` together in one
transaction. This is the deferred "rules.json parser + end-to-end loader wiring" piece
named in [the relate-definitions-rulesets design](2026-06-30-relate-definitions-rulesets-by-version-design.md):
once the loader always writes both rows together, every definition version that is
loaded also has a rule set.

## Decisions

- **Both flags required.** Every load supplies `--meta` and `--rules`. The previous
  meta-only invocation is no longer valid. This enforces "a loaded definition has a
  rule set" at the loader level (the DB still only enforces the reverse direction).
- **Atomic.** Both files are parsed and validated *before* any database write. If
  either is invalid, nothing is written. The definition and rule-set rows are committed
  in a single `SaveChangesAsync` (one transaction).
- **No consistency check** against the meta's informational `"rules"` field. The
  explicit `--rules` flag is the sole source of the rules path.
- **Single save outcome.** `SaveAsync` keeps its existing `Inserted`/`Updated` result,
  reflecting the definition (the principal of the 1:1 relationship).

## Key finding: the authored format differs from the storage format

The hand-authored `docs/strategies/<id>/rules.json` is a human-friendly shape that does
**not** match the storage jsonb shape that `StrategyRuleSetSerializer` reads and writes.
The serializer therefore cannot parse the authored file — a dedicated **authoring
parser** is required. This is the bulk of the work.

| Aspect | Authored `rules.json` | Storage jsonb (`StrategyRuleSetSerializer`) |
| --- | --- | --- |
| `direction` | string `"long-only"` | int enum code `1` |
| `parameters` / `indicators` | keyed **maps** (`"rsiPeriod": {…}`) | **arrays** of objects carrying `name` |
| operands | bare tokens: `"$oversold"`, `"close"`, `"emaSlow"`, `14` | `{ kind, number \| name \| field }` objects |
| `risk.positionSizing.type` | string `"percent-equity"` | int enum code |
| `risk.stopLoss.type` | string `"percent"` | int enum code |
| top-level `id` / `version` | present | absent (identity is the row key) |

The authoring parser maps the left column into the domain model; the storage serializer
(unchanged) then writes the right column when the row is persisted.

## Flow (`Program.cs`)

1. Parse args → `MetaPath` + `RulesPath` (both required).
2. `StrategyMetaParser.ParseFileAsync(MetaPath)` → `StrategyDefinition`.
3. `StrategyRulesParser.ParseFileAsync(RulesPath, definition.VersionId)` → `StrategyRuleSet`.
4. Steps 2–3 complete (parsed + validated) before any DB work, so a bad `rules.json`
   writes nothing.
5. `repository.SaveAsync(definition, ruleSet)` — upserts both rows in one
   `SaveChangesAsync` (one transaction).
6. Print outcome: `{outcome} strategy {VersionId} — {Name} ({MetaPath} + {RulesPath}).`

## Component changes

### 1. `StrategyLoaderArgs` — `Loader/Configuration/StrategyLoaderArgs.cs`

Add a `RulesPath` member and a `RulesFlag = "--rules"` constant. Rewrite `Parse` to:

- accept `--meta <path>` and `--rules <path>` in **any order**;
- require **both**; report a clear error naming whichever is missing;
- reject an unknown argument, a flag missing its value, and a duplicated flag.

```csharp
public sealed record StrategyLoaderArgs(string MetaPath, string RulesPath)
{
    public const string MetaFlag = "--meta";
    public const string RulesFlag = "--rules";
    // Parse: walk flag/value pairs, fill metaPath/rulesPath, error on
    // unknown flag, missing value, duplicate flag, or either path still null.
}
```

### 2. `StrategyRulesParser` (NEW) — `Loader/Configuration/StrategyRulesParser.cs`

Mirrors `StrategyMetaParser` in style: tolerant `JsonSerializerOptions`
(`PropertyNameCaseInsensitive`, `ReadCommentHandling = Skip`, `AllowTrailingCommas`),
throws `StrategyMetaException` on malformed input.

```csharp
public static Task<StrategyRuleSet> ParseFileAsync(
    string path, StrategyVersionId versionId, CancellationToken ct = default);

public static StrategyRuleSet Parse(string json, StrategyVersionId versionId);
```

Mapping from the authored shape to the domain model:

- `direction`: `"long-only" | "short-only" | "both"` → `TradeDirection`.
- `parameters` map: each entry `{ default, min, max, step }` → `ParameterSpec(name, …)`,
  name taken from the map key.
- `indicators` map: each entry `{ type, period, source }` →
  `IndicatorSpec(name, IndicatorKind, Operand period, PriceSource source)`; `type`
  string → `IndicatorKind` (`RSI`→`Rsi`, `EMA`→`Ema`); `source` string → `PriceSource`.
- `entry` (`all`) / `exit` (`any`) → `SignalRule(LogicalOperator, Condition[])`; each
  `{ left, op, right }` → `Condition(Operand, ComparisonOperator, Operand)`.
- **Operand tokens:** `"$name"` → `Operand.Parameter(name)`;
  `open|high|low|close|volume` (case-insensitive) → `Operand.Price(...)`;
  a JSON number → `Operand.Literal(...)`; any other string → `Operand.Indicator(name)`.
- `op` strings → `ComparisonOperator` (`<`, `<=`, `>`, `>=`, `==`/`=`, `!=`,
  `crosses-above`, `crosses-below`).
- `risk` → `RiskRules`: `positionSizing { type, value }` (`"percent-equity"`→
  `PercentEquity`, `"fixed-notional"`→`FixedNotional`); optional `stopLoss` / `takeProfit`
  `{ type, value }` (`"percent"`→`Percent`, `"atr"`→`Atr`), absent → `null`;
  `maxOpenPositions`, `maxOpenPerSymbol`.

Domain constructors enforce the value invariants (envelope ordering, positive
steps/values, non-empty rules, distinct names). **Error typing:** the parser throws
`StrategyMetaException` only for its own format/mapping errors (invalid JSON, missing
required block, unknown `direction`/`type`/`op`/`source`/operand token); domain-invariant
violations surface as the `StrategyDomainException` the constructor raises, unwrapped.
Both are caught by `Program`'s catch-all and printed as `Load failed: …`. The top-level
`id`/`version` in the file are ignored; identity comes from `versionId`.

### 3. Repository — `Loader/Persistence/StrategyDefinitionRepository.cs` + interface

Change the contract from definition-only to definition + rule set:

```csharp
Task<StrategySaveOutcome> SaveAsync(
    StrategyDefinition definition,
    StrategyRuleSet ruleSet,
    CancellationToken cancellationToken = default);
```

Implementation:

- Upsert the definition exactly as today (insert when `(id, version)` absent, else
  refresh the mutable descriptive fields).
- Upsert the rule set keyed by the same `(id, version)`: look up the existing
  `StrategyRuleSetRow`; insert `StrategyRuleSetRow.From(ruleSet)` when absent, else
  refresh its `Rules` jsonb. (Handles a pre-existing definition that has no rule set
  row yet.)
- A single `SaveChangesAsync` commits both rows in one transaction.
- Return `Inserted`/`Updated` based on the definition.

### 4. `Program.cs`

Wire steps 3–6 of the flow. Output line includes both paths.

## Deliberate non-goals (YAGNI)

- **No cross-reference validation** — e.g. that `$oversold` names a declared parameter,
  or that `emaSlow` names a declared indicator. The domain does not enforce this today;
  the parser matches that scope. Can be a follow-up.
- **No consistency check** against the meta `"rules"` field.
- **No change** to `StrategyRuleSetSerializer` or the storage jsonb shape.

## Tests (repo 100%-coverage rule)

- **`StrategyLoaderArgs.Parse`**: both flags in either order; missing `--meta`; missing
  `--rules`; unknown argument; flag with no value; duplicated flag.
- **`StrategyRulesParser`**: parses the rsi-movement `rules.json` into the expected
  `StrategyRuleSet`; each operand kind (parameter / price / literal / indicator); both
  logical modes (`all`/`any`); every `op`, sizing, and bracket string mapping; optional
  `stopLoss`/`takeProfit` present and absent; malformed JSON; unknown enum strings;
  domain-invariant violations surface as loader errors.
- **`StrategyDefinitionRepository.SaveAsync`**: insert both rows; idempotent re-run
  updates both; rule set inserted when the definition pre-existed without one.

## Acceptance criteria

- `loader --meta <m> --rules <r>` parses both files and persists a `StrategyDefinition`
  and its `StrategyRuleSet` in one transaction.
- Both flags are required and order-independent; missing/unknown/duplicate flags produce
  clear errors.
- An invalid `rules.json` (or `meta.json`) writes nothing.
- Re-running the same bundle is idempotent (both rows refreshed in place).
- The rsi-movement bundle loads end to end.
- Solution builds; tests pass at 100% coverage.
