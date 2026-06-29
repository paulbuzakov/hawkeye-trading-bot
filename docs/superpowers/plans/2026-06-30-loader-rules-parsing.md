# Loader `--rules` Parsing & Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the strategy loader from `loader --meta <path>` to `loader --meta <path> --rules <path>`, parsing the hand-authored `rules.json` into a `StrategyRuleSet` and persisting the definition and its rule set together in one transaction.

**Architecture:** A new authoring parser (`StrategyRulesParser`) maps the human-friendly authored shape (string enums, keyed maps, bare operand tokens) into the domain model; the existing storage serializer (unchanged) writes the jsonb on persist. `StrategyLoaderArgs` gains a required `--rules` flag. The repository's `SaveAsync` takes both the definition and the rule set and upserts both rows in a single `SaveChangesAsync`. `Program` wires the flow and both files are fully parsed/validated before any DB write, so a bad `rules.json` writes nothing.

**Tech Stack:** .NET 10, C#, `System.Text.Json` (`JsonDocument` for the union-typed authored shape), EF Core 10 + Npgsql.

**Source of truth:** the approved design `docs/superpowers/specs/2026-06-30-loader-rules-parsing-design.md`.

## Global Constraints

- **No test project / no TDD this branch.** Per maintainer decision, the strategy test suite stays removed; the `--rules` work mirrors that precedent. Verify each task with `dotnet build`, and verify the feature end-to-end by running the loader on the rsi-movement bundle. (This deliberately diverges from the design doc's "tests pass at 100% coverage" criterion and the repo's coverage rule.)
- **Both flags required.** Every load supplies `--meta` and `--rules`, order-independent. Missing/unknown/duplicate flags produce clear errors.
- **Atomic.** Both files are parsed and validated before any DB write; definition + rule-set rows commit in one `SaveChangesAsync`.
- **Error typing.** The parser throws `StrategyMetaException` for its own format/mapping errors (invalid JSON, missing block, unknown `direction`/`type`/`op`/`source`/operand token). Domain-invariant violations surface as the `StrategyDomainException` the domain constructors raise, unwrapped. Both are caught by `Program`'s catch-all and printed as `Load failed: …`.
- **Identity from `versionId`.** Any top-level `id`/`version` in `rules.json` is ignored; identity comes from the supplied `StrategyVersionId`.
- **No cross-reference validation** (`$oversold` resolves to a declared parameter, `emaSlow` to a declared indicator) — out of scope, matches the domain's current scope.
- **Build command:** `dotnet build src/strategy/HTB.Strategy.Loader/HTB.Strategy.Loader.csproj -v q --nologo`. The repo's `HTB.slnx` still references the removed `tests/strategy/HTB.Strategy.Shared.Tests` project, so build the loader project directly, not the solution.

---

### Task 1: Require the `--rules` flag in `StrategyLoaderArgs`

**Files:**
- Modify: `src/strategy/HTB.Strategy.Loader/Configuration/StrategyLoaderArgs.cs`

**Interfaces:**
- Produces: `StrategyLoaderArgs(string MetaPath, string RulesPath)` record; constants `MetaFlag = "--meta"`, `RulesFlag = "--rules"`; `static StrategyLoaderArgs Parse(IReadOnlyList<string> args)`.
- Consumes: `StrategyMetaException` (existing, same namespace).

- [ ] **Step 1: Replace the file contents**

```csharp
namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Parsed command-line arguments for the strategy loader. Two required flags:
/// <c>--meta &lt;path&gt;</c> pointing at a bundle's <c>meta.json</c>, and
/// <c>--rules &lt;path&gt;</c> pointing at its <c>rules.json</c>. Flags may appear in either
/// order; each must appear exactly once.
/// </summary>
public sealed record StrategyLoaderArgs(string MetaPath, string RulesPath)
{
    public const string MetaFlag = "--meta";
    public const string RulesFlag = "--rules";

    public static StrategyLoaderArgs Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? metaPath = null;
        string? rulesPath = null;

        for (var i = 0; i < args.Count; i++)
        {
            var flag = args[i];
            if (flag != MetaFlag && flag != RulesFlag)
            {
                throw new StrategyMetaException(
                    $"Unknown argument \"{flag}\". Usage: {MetaFlag} <path> {RulesFlag} <path>."
                );
            }

            if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                throw new StrategyMetaException($"{flag} requires a file path argument.");
            }

            var value = args[i + 1];
            i++;

            if (flag == MetaFlag)
            {
                if (metaPath is not null)
                {
                    throw new StrategyMetaException($"{MetaFlag} was specified more than once.");
                }

                metaPath = value;
            }
            else
            {
                if (rulesPath is not null)
                {
                    throw new StrategyMetaException($"{RulesFlag} was specified more than once.");
                }

                rulesPath = value;
            }
        }

        if (metaPath is null)
        {
            throw new StrategyMetaException($"Missing required argument {MetaFlag} <path>.");
        }

        if (rulesPath is null)
        {
            throw new StrategyMetaException($"Missing required argument {RulesFlag} <path>.");
        }

        return new StrategyLoaderArgs(metaPath, rulesPath);
    }
}
```

- [ ] **Step 2: Build to verify it compiles** (will warn/fail at `Program.cs` if that still calls the old single-arg shape — that's fixed in Task 4; this task's file compiles on its own, the project link errors resolve by Task 4)

Run: `dotnet build src/strategy/HTB.Strategy.Loader/HTB.Strategy.Loader.csproj -v q --nologo`
Expected: the only errors, if any, are `Program.cs` referencing `parsed.RulesPath`/`SaveAsync` shapes not yet wired. If you see errors *inside `StrategyLoaderArgs.cs`*, fix them before moving on.

- [ ] **Step 3: Commit**

```bash
git add src/strategy/HTB.Strategy.Loader/Configuration/StrategyLoaderArgs.cs
git commit -m "feat(strategy): require --rules flag in loader args"
```

---

### Task 2: Add the `StrategyRulesParser` authoring parser

**Files:**
- Create: `src/strategy/HTB.Strategy.Loader/Configuration/StrategyRulesParser.cs`
- Modify: `src/strategy/HTB.Strategy.Loader/Configuration/StrategyMetaException.cs` (doc comment only)

**Interfaces:**
- Consumes (domain, from `HTB.Strategy.Shared.Domain`): `StrategyRuleSet(StrategyVersionId, TradeDirection, IReadOnlyList<ParameterSpec>, IReadOnlyList<IndicatorSpec>, SignalRule entry, SignalRule exit, RiskRules)`; `ParameterSpec(string name, decimal @default, decimal min, decimal max, decimal step)`; `IndicatorSpec(string name, IndicatorKind, Operand period, PriceSource source)`; `Operand.Literal(decimal)`, `Operand.Parameter(string)`, `Operand.Indicator(string)`, `Operand.Price(PriceSource)`; `SignalRule(LogicalOperator, IReadOnlyList<Condition>)`; `Condition(Operand left, ComparisonOperator, Operand right)`; `RiskRules(PositionSizing, ProtectiveExit? stopLoss, ProtectiveExit? takeProfit, int maxOpenPositions, int maxOpenPerSymbol)`; `PositionSizing(SizingMethod, decimal)`; `ProtectiveExit(BracketType, decimal)`. Enums: `TradeDirection {LongOnly, ShortOnly, Both}`, `IndicatorKind {Rsi, Ema}`, `PriceSource {Open, High, Low, Close, Volume}`, `LogicalOperator {All, Any}`, `ComparisonOperator {LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual, Equal, NotEqual, CrossesAbove, CrossesBelow}`, `SizingMethod {PercentEquity, FixedNotional}`, `BracketType {Percent, Atr}`. Also `StrategyVersionId` and `StrategyMetaException`.
- Produces: `static Task<StrategyRuleSet> StrategyRulesParser.ParseFileAsync(string path, StrategyVersionId versionId, CancellationToken ct = default)`; `static StrategyRuleSet StrategyRulesParser.Parse(string json, StrategyVersionId versionId)`.

- [ ] **Step 1: Create `StrategyRulesParser.cs` with the full parser**

```csharp
using System.Text.Json;
using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Parses and validates a strategy bundle's hand-authored <c>rules.json</c> into a
/// <see cref="StrategyRuleSet"/>. The authored shape is human-friendly and differs from the
/// storage jsonb shape (string enums, keyed maps, bare operand tokens), so this is a dedicated
/// authoring parser rather than the storage serializer. Identity comes from the supplied
/// <paramref name="versionId"/>; any top-level <c>id</c>/<c>version</c> in the file is ignored.
/// Comments and trailing commas are tolerated. Format/mapping errors throw
/// <see cref="StrategyMetaException"/>; value-invariant violations surface as the
/// <see cref="StrategyDomainException"/> the domain constructors raise.
/// </summary>
public static class StrategyRulesParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads and parses the <c>rules.json</c> at <paramref name="path"/>.</summary>
    public static async Task<StrategyRuleSet> ParseFileAsync(
        string path,
        StrategyVersionId versionId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return Parse(json, versionId);
    }

    /// <summary>Parses a <c>rules.json</c> document from a JSON string.</summary>
    public static StrategyRuleSet Parse(string json, StrategyVersionId versionId)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new StrategyMetaException($"rules.json is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new StrategyMetaException("rules.json must contain a rules object.");
            }

            var direction = ParseDirection(GetRequiredString(root, "direction"));
            var parameters = ParseParameters(root);
            var indicators = ParseIndicators(root);
            var entry = ParseSignal(root, "entry");
            var exit = ParseSignal(root, "exit");
            var risk = ParseRisk(root);

            return new StrategyRuleSet(versionId, direction, parameters, indicators, entry, exit, risk);
        }
    }

    private static TradeDirection ParseDirection(string raw) =>
        raw.ToLowerInvariant() switch
        {
            "long-only" => TradeDirection.LongOnly,
            "short-only" => TradeDirection.ShortOnly,
            "both" => TradeDirection.Both,
            _ => throw new StrategyMetaException($"rules.json has an unknown direction \"{raw}\"."),
        };

    private static IReadOnlyList<ParameterSpec> ParseParameters(JsonElement root)
    {
        var block = GetRequired(root, "parameters");
        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyMetaException("rules.json \"parameters\" must be an object.");
        }

        var parameters = new List<ParameterSpec>();
        foreach (var entry in block.EnumerateObject())
        {
            var spec = entry.Value;
            parameters.Add(
                new ParameterSpec(
                    entry.Name,
                    GetRequiredNumber(spec, "default", entry.Name),
                    GetRequiredNumber(spec, "min", entry.Name),
                    GetRequiredNumber(spec, "max", entry.Name),
                    GetRequiredNumber(spec, "step", entry.Name)
                )
            );
        }

        return parameters;
    }

    private static IReadOnlyList<IndicatorSpec> ParseIndicators(JsonElement root)
    {
        var block = GetRequired(root, "indicators");
        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyMetaException("rules.json \"indicators\" must be an object.");
        }

        var indicators = new List<IndicatorSpec>();
        foreach (var entry in block.EnumerateObject())
        {
            var spec = entry.Value;
            indicators.Add(
                new IndicatorSpec(
                    entry.Name,
                    ParseIndicatorKind(GetRequiredString(spec, "type"), entry.Name),
                    ParseOperand(GetRequired(spec, "period"), $"indicator \"{entry.Name}\" period"),
                    ParsePriceSource(GetRequiredString(spec, "source"), entry.Name)
                )
            );
        }

        return indicators;
    }

    private static IndicatorKind ParseIndicatorKind(string raw, string owner) =>
        raw.ToUpperInvariant() switch
        {
            "RSI" => IndicatorKind.Rsi,
            "EMA" => IndicatorKind.Ema,
            _ => throw new StrategyMetaException($"indicator \"{owner}\" has an unknown type \"{raw}\"."),
        };

    private static PriceSource ParsePriceSource(string raw, string owner)
    {
        if (!TryParsePriceSource(raw, out var field))
        {
            throw new StrategyMetaException($"indicator \"{owner}\" has an unknown source \"{raw}\".");
        }

        return field;
    }

    private static bool TryParsePriceSource(string token, out PriceSource field)
    {
        switch (token.ToLowerInvariant())
        {
            case "open":
                field = PriceSource.Open;
                return true;
            case "high":
                field = PriceSource.High;
                return true;
            case "low":
                field = PriceSource.Low;
                return true;
            case "close":
                field = PriceSource.Close;
                return true;
            case "volume":
                field = PriceSource.Volume;
                return true;
            default:
                field = default;
                return false;
        }
    }

    private static Operand ParseOperand(JsonElement element, string context)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return Operand.Literal(element.GetDecimal());
            case JsonValueKind.String:
                var token = element.GetString();
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new StrategyMetaException($"rules.json {context} has an empty operand.");
                }

                token = token.Trim();
                if (token.StartsWith('$'))
                {
                    return Operand.Parameter(token[1..]);
                }

                return TryParsePriceSource(token, out var field)
                    ? Operand.Price(field)
                    : Operand.Indicator(token);
            default:
                throw new StrategyMetaException(
                    $"rules.json {context} must be a number or a string operand token."
                );
        }
    }

    private static SignalRule ParseSignal(JsonElement root, string name)
    {
        var block = GetRequired(root, name);
        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyMetaException(
                $"rules.json \"{name}\" must be an object with an \"all\" or \"any\" list."
            );
        }

        var hasAll = TryGetProperty(block, "all", out var allList);
        var hasAny = TryGetProperty(block, "any", out var anyList);
        if (hasAll == hasAny)
        {
            throw new StrategyMetaException(
                $"rules.json \"{name}\" must declare exactly one of \"all\" or \"any\"."
            );
        }

        var mode = hasAll ? LogicalOperator.All : LogicalOperator.Any;
        var list = hasAll ? allList : anyList;
        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new StrategyMetaException($"rules.json \"{name}\" conditions must be an array.");
        }

        var conditions = new List<Condition>();
        foreach (var item in list.EnumerateArray())
        {
            conditions.Add(ParseCondition(item, name));
        }

        return new SignalRule(mode, conditions);
    }

    private static Condition ParseCondition(JsonElement element, string owner)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyMetaException($"rules.json \"{owner}\" condition must be an object.");
        }

        return new Condition(
            ParseOperand(GetRequired(element, "left"), $"\"{owner}\" condition left"),
            ParseComparison(GetRequiredString(element, "op"), owner),
            ParseOperand(GetRequired(element, "right"), $"\"{owner}\" condition right")
        );
    }

    private static ComparisonOperator ParseComparison(string raw, string owner) =>
        raw.ToLowerInvariant() switch
        {
            "<" => ComparisonOperator.LessThan,
            "<=" => ComparisonOperator.LessThanOrEqual,
            ">" => ComparisonOperator.GreaterThan,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "==" or "=" => ComparisonOperator.Equal,
            "!=" => ComparisonOperator.NotEqual,
            "crosses-above" => ComparisonOperator.CrossesAbove,
            "crosses-below" => ComparisonOperator.CrossesBelow,
            _ => throw new StrategyMetaException($"rules.json \"{owner}\" has an unknown operator \"{raw}\"."),
        };

    private static RiskRules ParseRisk(JsonElement root)
    {
        var block = GetRequired(root, "risk");
        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyMetaException("rules.json \"risk\" must be an object.");
        }

        return new RiskRules(
            ParsePositionSizing(GetRequired(block, "positionSizing")),
            ParseProtectiveExit(block, "stopLoss"),
            ParseProtectiveExit(block, "takeProfit"),
            GetRequiredInt(block, "maxOpenPositions"),
            GetRequiredInt(block, "maxOpenPerSymbol")
        );
    }

    private static PositionSizing ParsePositionSizing(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyMetaException("rules.json \"positionSizing\" must be an object.");
        }

        var method = GetRequiredString(element, "type").ToLowerInvariant() switch
        {
            "percent-equity" => SizingMethod.PercentEquity,
            "fixed-notional" => SizingMethod.FixedNotional,
            var other => throw new StrategyMetaException(
                $"rules.json has an unknown positionSizing type \"{other}\"."
            ),
        };

        return new PositionSizing(method, GetRequiredNumber(element, "value", "positionSizing"));
    }

    private static ProtectiveExit? ParseProtectiveExit(JsonElement risk, string name)
    {
        if (!TryGetProperty(risk, name, out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyMetaException($"rules.json \"{name}\" must be an object.");
        }

        var type = GetRequiredString(element, "type").ToLowerInvariant() switch
        {
            "percent" => BracketType.Percent,
            "atr" => BracketType.Atr,
            var other => throw new StrategyMetaException(
                $"rules.json \"{name}\" has an unknown type \"{other}\"."
            ),
        };

        return new ProtectiveExit(type, GetRequiredNumber(element, "value", name));
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static JsonElement GetRequired(JsonElement obj, string name)
    {
        if (!TryGetProperty(obj, name, out var value))
        {
            throw new StrategyMetaException($"rules.json is missing the required \"{name}\" block.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement obj, string name)
    {
        var value = GetRequired(obj, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new StrategyMetaException($"rules.json \"{name}\" must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static decimal GetRequiredNumber(JsonElement obj, string name, string owner)
    {
        if (!TryGetProperty(obj, name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new StrategyMetaException($"rules.json \"{owner}\" is missing a numeric \"{name}\".");
        }

        return value.GetDecimal();
    }

    private static int GetRequiredInt(JsonElement obj, string name)
    {
        if (
            !TryGetProperty(obj, name, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var result)
        )
        {
            throw new StrategyMetaException($"rules.json \"risk\" is missing an integer \"{name}\".");
        }

        return result;
    }
}
```

- [ ] **Step 2: Update `StrategyMetaException` doc comment to cover rules.json**

In `src/strategy/HTB.Strategy.Loader/Configuration/StrategyMetaException.cs`, replace the doc comment so it reads:

```csharp
namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Thrown when the loader's inputs are invalid: a missing/malformed <c>--meta</c> or
/// <c>--rules</c> argument, a <c>meta.json</c> that is not valid JSON or fails validation
/// (missing id/version/name, empty scope, unknown timeframe, negative warmup), or a
/// <c>rules.json</c> that is not valid JSON or fails format/mapping validation (missing block,
/// unknown direction/type/operator/source, or an unrecognized operand token).
/// </summary>
public sealed class StrategyMetaException(string message) : Exception(message);
```

- [ ] **Step 3: Build to verify the parser compiles**

Run: `dotnet build src/strategy/HTB.Strategy.Loader/HTB.Strategy.Loader.csproj -v q --nologo`
Expected: parser compiles; remaining errors (if any) are only in `Program.cs`/repository wiring fixed in Tasks 3–4.

- [ ] **Step 4: Commit**

```bash
git add src/strategy/HTB.Strategy.Loader/Configuration/StrategyRulesParser.cs src/strategy/HTB.Strategy.Loader/Configuration/StrategyMetaException.cs
git commit -m "feat(strategy): add rules.json authoring parser"
```

---

### Task 3: Persist definition + rule set together in the repository

**Files:**
- Modify: `src/strategy/HTB.Strategy.Loader/Persistence/IStrategyDefinitionRepository.cs`
- Modify: `src/strategy/HTB.Strategy.Loader/Persistence/StrategyDefinitionRepository.cs`

**Interfaces:**
- Consumes: `StrategyRuleSet` (domain), `StrategyRuleSetRow.From(StrategyRuleSet)` (from `HTB.Strategy.Shared.Persistence`), `_db.StrategyRuleSets` DbSet, existing `StrategySaveOutcome {Inserted, Updated}`.
- Produces: `Task<StrategySaveOutcome> SaveAsync(StrategyDefinition definition, StrategyRuleSet ruleSet, CancellationToken cancellationToken = default)` (replaces the definition-only overload).

- [ ] **Step 1: Update the interface**

Replace `src/strategy/HTB.Strategy.Loader/Persistence/IStrategyDefinitionRepository.cs` with:

```csharp
using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Loader.Persistence;

public interface IStrategyDefinitionRepository
{
    /// <summary>
    /// Persists <paramref name="definition"/> and its <paramref name="ruleSet"/> keyed by the same
    /// version id (<c>id + version</c>), in a single transaction: inserts new rows, or refreshes the
    /// definition's descriptive fields and the rule set's jsonb body for an existing version. The
    /// returned outcome reflects the definition (the principal of the 1:1 relationship).
    /// </summary>
    Task<StrategySaveOutcome> SaveAsync(
        StrategyDefinition definition,
        StrategyRuleSet ruleSet,
        CancellationToken cancellationToken = default
    );
}
```

- [ ] **Step 2: Update the implementation**

Replace `src/strategy/HTB.Strategy.Loader/Persistence/StrategyDefinitionRepository.cs` with:

```csharp
using HTB.Strategy.Shared.Domain;
using HTB.Strategy.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IStrategyDefinitionRepository"/>. Upserts a definition and
/// its rule set under the same <c>(id, version)</c> key: inserts when absent, otherwise refreshes
/// the definition's mutable descriptive fields and the rule set's jsonb body. Both rows commit in a
/// single <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> so loading a bundle is atomic
/// and idempotent.
/// </summary>
public sealed class StrategyDefinitionRepository(StrategyWriteDbContext db) : IStrategyDefinitionRepository
{
    private readonly StrategyWriteDbContext _db = db;

    public async Task<StrategySaveOutcome> SaveAsync(
        StrategyDefinition definition,
        StrategyRuleSet ruleSet,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(ruleSet);

        var id = definition.Id;
        var version = definition.Version;

        var existing = await _db.StrategyDefinitions.FirstOrDefaultAsync(
            d => d.Id == id && d.Version == version,
            cancellationToken
        );

        StrategySaveOutcome outcome;
        if (existing is null)
        {
            _db.StrategyDefinitions.Add(definition);
            outcome = StrategySaveOutcome.Inserted;
        }
        else
        {
            existing.Name = definition.Name;
            existing.Description = definition.Description;
            existing.Tags = definition.Tags;
            existing.Exchanges = definition.Exchanges;
            existing.Symbols = definition.Symbols;
            existing.Timeframes = definition.Timeframes;
            existing.WarmupBars = definition.WarmupBars;
            outcome = StrategySaveOutcome.Updated;
        }

        await UpsertRuleSetAsync(id, version, ruleSet, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return outcome;
    }

    private async Task UpsertRuleSetAsync(
        StrategyId id,
        StrategyVersion version,
        StrategyRuleSet ruleSet,
        CancellationToken cancellationToken
    )
    {
        var serialized = StrategyRuleSetRow.From(ruleSet);

        var existingRow = await _db.StrategyRuleSets.FirstOrDefaultAsync(
            r => r.Id == id && r.Version == version,
            cancellationToken
        );

        if (existingRow is null)
        {
            _db.StrategyRuleSets.Add(serialized);
        }
        else
        {
            existingRow.Rules = serialized.Rules;
        }
    }
}
```

- [ ] **Step 3: Build to verify the repository compiles**

Run: `dotnet build src/strategy/HTB.Strategy.Loader/HTB.Strategy.Loader.csproj -v q --nologo`
Expected: only `Program.cs` errors remain (it still calls the one-arg `SaveAsync`), fixed in Task 4.

- [ ] **Step 4: Commit**

```bash
git add src/strategy/HTB.Strategy.Loader/Persistence/IStrategyDefinitionRepository.cs src/strategy/HTB.Strategy.Loader/Persistence/StrategyDefinitionRepository.cs
git commit -m "feat(strategy): persist definition and rule set together in repository"
```

---

### Task 4: Wire the flow in `Program.cs`

**Files:**
- Modify: `src/strategy/HTB.Strategy.Loader/Program.cs`

**Interfaces:**
- Consumes: `StrategyLoaderArgs.Parse`, `parsed.MetaPath`, `parsed.RulesPath`, `StrategyMetaParser.ParseFileAsync`, `StrategyRulesParser.ParseFileAsync(path, versionId)`, `definition.VersionId`, `repository.SaveAsync(definition, ruleSet)`.

- [ ] **Step 1: Replace the `Main` body's parse + save + output lines**

In `src/strategy/HTB.Strategy.Loader/Program.cs`, change the `try` block so that, after parsing args and the definition, it also parses the rule set, saves both, and prints both paths. The relevant lines become:

```csharp
            var parsed = StrategyLoaderArgs.Parse(args);
            var definition = await StrategyMetaParser.ParseFileAsync(parsed.MetaPath);
            var ruleSet = await StrategyRulesParser.ParseFileAsync(parsed.RulesPath, definition.VersionId);

            var connectionString =
                Environment.GetEnvironmentVariable(ConnectionStringEnvVar) ?? DefaultConnectionString;

            var options = new DbContextOptionsBuilder<StrategyWriteDbContext>().UseNpgsql(connectionString).Options;

            await using var db = new StrategyWriteDbContext(options);
            var repository = new StrategyDefinitionRepository(db);
            var outcome = await repository.SaveAsync(definition, ruleSet);

            Console.WriteLine(
                $"{outcome} strategy {definition.VersionId} — {definition.Name} "
                    + $"({parsed.MetaPath} + {parsed.RulesPath})."
            );
            return 0;
```

Update the class doc comment's first sentence to mention both files:

```csharp
/// Console entry point for the strategy loader. Reads a strategy bundle's <c>meta.json</c> via
/// <c>--meta &lt;path&gt;</c> and its <c>rules.json</c> via <c>--rules &lt;path&gt;</c>, parses
/// them into a <c>StrategyDefinition</c> and a <c>StrategyRuleSet</c>, and persists both to the
/// strategy store in one transaction. Pure composition (arg wiring + PostgreSQL wiring + console
/// output), so it is excluded from coverage; the testable logic lives in the parsers and the repository.
```

- [ ] **Step 2: Build the whole loader project — it must now compile cleanly**

Run: `dotnet build src/strategy/HTB.Strategy.Loader/HTB.Strategy.Loader.csproj -v q --nologo`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add src/strategy/HTB.Strategy.Loader/Program.cs
git commit -m "feat(strategy): wire --rules parsing and dual persistence in loader"
```

---

### Task 5: End-to-end verification on the rsi-movement bundle

**Files:** none (verification only).

**Prerequisite:** a reachable PostgreSQL with the strategy schema migrated. The loader defaults to `Host=localhost;Port=5432;Database=hawkeye;Username=hawkeye;Password=hawkeye;` and honors `HTB_CONNECTION_STRING`. The `strategy.strategy_rule_sets` table comes from the `AddStrategyRuleSets` migration already on this branch — apply migrations first if the DB is fresh.

- [ ] **Step 1: Argument-handling smoke checks (no DB needed — these fail before any DB call)**

Run each and confirm the error message:

```bash
dotnet run --project src/strategy/HTB.Strategy.Loader -- --meta docs/strategies/rsi-movement/meta.json
# Expected stderr: Load failed: Missing required argument --rules <path>.

dotnet run --project src/strategy/HTB.Strategy.Loader -- --rules docs/strategies/rsi-movement/rules.json
# Expected stderr: Load failed: Missing required argument --meta <path>.

dotnet run --project src/strategy/HTB.Strategy.Loader -- --meta a --meta b --rules r
# Expected stderr: Load failed: --meta was specified more than once.

dotnet run --project src/strategy/HTB.Strategy.Loader -- --bogus x
# Expected stderr: Load failed: Unknown argument "--bogus". Usage: --meta <path> --rules <path>.
```

- [ ] **Step 2: Full end-to-end load (requires PostgreSQL)**

Run:

```bash
dotnet run --project src/strategy/HTB.Strategy.Loader -- \
  --meta docs/strategies/rsi-movement/meta.json \
  --rules docs/strategies/rsi-movement/rules.json
```

Expected stdout:

```
Inserted strategy rsi-movement@1 — RSI Movement (docs/strategies/rsi-movement/meta.json + docs/strategies/rsi-movement/rules.json).
```

- [ ] **Step 3: Idempotency check — run the exact same command again**

Expected stdout (now `Updated`):

```
Updated strategy rsi-movement@1 — RSI Movement (docs/strategies/rsi-movement/meta.json + docs/strategies/rsi-movement/rules.json).
```

- [ ] **Step 4: Confirm both rows exist and the jsonb round-trips**

Run (adjust `psql` connection to match):

```bash
psql "$HTB_CONNECTION_STRING" -c "select id, version from strategy.strategy_definitions where id='rsi-movement';"
psql "$HTB_CONNECTION_STRING" -c "select id, version, jsonb_typeof(rules) as rules_kind, rules->>'direction' as direction from strategy.strategy_rule_sets where id='rsi-movement';"
```

Expected: one definition row `rsi-movement | 1`; one rule-set row `rsi-movement | 1 | object | 1` (direction `1` = `LongOnly`, the stored int code).

> If no PostgreSQL is available in this environment, complete Step 1 (which exercises args parsing through `Program`) and record that Steps 2–4 require a database — do **not** claim end-to-end persistence works without running them.

---

## Self-Review

**Spec coverage** (against `2026-06-30-loader-rules-parsing-design.md`):
- Component change 1 (`StrategyLoaderArgs`, both flags, any order, unknown/missing/duplicate errors) → Task 1. ✓
- Component change 2 (`StrategyRulesParser`: direction, parameter/indicator maps, operand tokens, op/sizing/bracket mappings, optional stopLoss/takeProfit, error typing, ignore top-level id/version) → Task 2. ✓
- Component change 3 (repository `SaveAsync(definition, ruleSet)`, upsert both, single SaveChanges, outcome from definition, rule set inserted when definition pre-existed) → Task 3. ✓
- Component change 4 (`Program.cs` flow, parse-before-write atomicity, output with both paths) → Task 4. ✓
- Acceptance criteria (loads both rows in one transaction; flags required & order-independent; invalid file writes nothing; idempotent re-run; rsi-movement loads end to end) → Task 5. ✓
- Deviation: the design's "tests pass at 100% coverage" is intentionally waived per the Global Constraints (maintainer decision); replaced by build + end-to-end run verification.

**Operand token coverage:** parameter (`$oversold`), indicator (`emaSlow`), price field (`close`), literal (numbers in `period`/risk) — all handled by `ParseOperand`/`TryParsePriceSource`. ✓

**Type consistency:** `SaveAsync(definition, ruleSet)` signature matches between interface (Task 3 Step 1), implementation (Step 2), and call site (Task 4 Step 1). `ParseFileAsync(path, versionId)` matches between parser (Task 2) and call site (Task 4). `StrategyRuleSetRow.From` returns a row carrying `Id`/`Version`/`Rules` from `ruleSet.VersionId`, matching the upsert's key lookup. ✓
