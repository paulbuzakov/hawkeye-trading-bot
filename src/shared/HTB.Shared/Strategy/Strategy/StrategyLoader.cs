using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Domain.Conditions;
using HTB.Shared.Strategy.Strategy.Indicators;

namespace HTB.Shared.Strategy.Strategy;

/// <summary>
/// Turns the untrusted <c>meta.json</c> + <c>rules.json</c> pair into a validated, hashed,
/// immutable <see cref="StrategyDefinition"/> (Builder/Factory). Every validation failure surfaces
/// as a typed <see cref="StrategyConfigException"/>.
/// </summary>
public sealed class StrategyLoader : IStrategyLoader
{
    private const string SupportedSchemaVersion = "1.0";

    private static readonly IReadOnlySet<string> RuleKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "entry-long",
        "exit-long",
        "entry-short",
        "exit-short",
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <inheritdoc />
    public StrategyDefinition Load(string manifestJson, string rulesJson)
    {
        ArgumentNullException.ThrowIfNull(manifestJson);
        ArgumentNullException.ThrowIfNull(rulesJson);

        var meta = Deserialize<MetaDto>(manifestJson, "meta.json");
        var (manifest, parameters) = BuildManifest(meta);

        var rulesDto = Deserialize<RulesDto>(rulesJson, "rules.json");
        ValidateRulesIdentity(rulesDto, manifest);

        var isRunnable = VerifyHashAndRunnability(manifest, rulesJson);

        var indicators = CompileIndicators(rulesDto.Indicators, parameters);
        var indicatorIds = indicators.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var parameterNames = manifest.Parameters.Keys.ToHashSet(StringComparer.Ordinal);
        var rules = CompileRules(rulesDto.Rules, parameterNames, indicatorIds);
        var requestedRisk = BuildRequestedRisk(rulesDto.RequestedRisk);
        var execution = BuildExecution(rulesDto.Execution);

        var strategyRules = new StrategyRules(
            manifest.SchemaVersion,
            manifest.Id,
            manifest.Version.Number,
            indicators,
            rules,
            requestedRisk,
            execution
        );

        return new StrategyDefinition(manifest, strategyRules, parameters, isRunnable);
    }

    private static T Deserialize<T>(string json, string what)
    {
        T? dto;
        try
        {
            dto = JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new StrategyConfigException($"{what} is not valid JSON: {ex.Message}");
        }

        return dto ?? throw new StrategyConfigException($"{what} must be a JSON object.");
    }

    // ---- meta.json -------------------------------------------------------

    private static (StrategyManifest Manifest, IReadOnlyDictionary<string, decimal> Parameters) BuildManifest(
        MetaDto meta
    )
    {
        RequireSchemaVersion(meta.SchemaVersion, "meta.json");
        RequireNonEmpty(meta.Id, "meta.json \"id\"");
        RequireNonEmpty(meta.Name, "meta.json \"name\"");

        if (meta.Deterministic != true)
        {
            throw new StrategyConfigException("meta.json \"deterministic\" must be true for schema 1.0.");
        }

        var version = BuildVersion(meta.Version);
        var applicability = BuildApplicability(meta.Applicability);
        var (specs, parameters) = BuildParameters(meta.Parameters);

        var manifest = new StrategyManifest(
            meta.SchemaVersion!,
            meta.Id!,
            meta.Name!,
            meta.Description ?? string.Empty,
            meta.Author ?? string.Empty,
            true,
            version,
            applicability,
            specs,
            meta.Tags ?? []
        );

        return (manifest, parameters);
    }

    private static StrategyVersion BuildVersion(VersionDto? dto)
    {
        if (dto is null)
        {
            throw new StrategyConfigException("meta.json is missing a \"version\" object.");
        }

        if (dto.Number is not { } number || number < 1)
        {
            throw new StrategyConfigException("meta.json \"version.number\" must be a positive integer.");
        }

        var status = dto.Status?.ToLowerInvariant() switch
        {
            "draft" => StrategyStatus.Draft,
            "active" => StrategyStatus.Active,
            "retired" => StrategyStatus.Retired,
            _ => throw new StrategyConfigException(
                $"meta.json \"version.status\" is invalid: \"{dto.Status}\" (expected draft|active|retired)."
            ),
        };

        RequireNonEmpty(dto.RulesHash, "meta.json \"version.rules-hash\"");

        return new StrategyVersion(number, status, dto.CreatedAt, dto.RulesHash!);
    }

    private static Applicability BuildApplicability(ApplicabilityDto? dto)
    {
        if (dto is null)
        {
            throw new StrategyConfigException("meta.json is missing an \"applicability\" object.");
        }

        if (string.IsNullOrWhiteSpace(dto.Timeframe)
            || !Enum.TryParse<Timeframe>(dto.Timeframe, ignoreCase: true, out var timeframe)
            || !Enum.IsDefined(timeframe))
        {
            throw new StrategyConfigException(
                $"meta.json \"applicability.timeframe\" is invalid: \"{dto.Timeframe}\"."
            );
        }

        if (dto.WarmupBars is not { } warmup || warmup < 0)
        {
            throw new StrategyConfigException(
                "meta.json \"applicability.warmup-bars\" must be a non-negative integer."
            );
        }

        return new Applicability(dto.Exchanges ?? [], dto.Symbols ?? [], timeframe, warmup);
    }

    private static (IReadOnlyDictionary<string, ParameterSpec> Specs, IReadOnlyDictionary<string, decimal> Values)
        BuildParameters(Dictionary<string, ParameterSpecDto>? dto)
    {
        var specs = new Dictionary<string, ParameterSpec>(StringComparer.Ordinal);
        var values = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (dto is null)
        {
            return (specs, values);
        }

        foreach (var (name, spec) in dto)
        {
            if (spec is null)
            {
                throw new StrategyConfigException($"Parameter \"{name}\" has no specification.");
            }

            var type = spec.Type?.ToLowerInvariant() switch
            {
                "int" => ParameterType.Int,
                "decimal" => ParameterType.Decimal,
                _ => throw new StrategyConfigException(
                    $"Parameter \"{name}\" has an invalid type \"{spec.Type}\" (expected int|decimal)."
                ),
            };

            if (spec.Default is not { } def || spec.Min is not { } min || spec.Max is not { } max)
            {
                throw new StrategyConfigException(
                    $"Parameter \"{name}\" must declare numeric default, min and max."
                );
            }

            if (min > max)
            {
                throw new StrategyConfigException($"Parameter \"{name}\" has min greater than max.");
            }

            if (def < min || def > max)
            {
                throw new StrategyConfigException(
                    $"Parameter \"{name}\" default {def} is outside [{min}, {max}]."
                );
            }

            if (type == ParameterType.Int && (!IsIntegral(def) || !IsIntegral(min) || !IsIntegral(max)))
            {
                throw new StrategyConfigException(
                    $"Parameter \"{name}\" is typed int but has non-integral default/min/max."
                );
            }

            specs[name] = new ParameterSpec(type, def, min, max);
            values[name] = def;
        }

        return (specs, values);
    }

    // ---- rules.json identity, hash ---------------------------------------

    private static void ValidateRulesIdentity(RulesDto rules, StrategyManifest manifest)
    {
        RequireSchemaVersion(rules.SchemaVersion, "rules.json");

        if (!string.Equals(rules.StrategyId, manifest.Id, StringComparison.Ordinal))
        {
            throw new StrategyConfigException(
                $"rules.json \"strategy-id\" (\"{rules.StrategyId}\") must equal meta.json \"id\" (\"{manifest.Id}\")."
            );
        }

        if (rules.VersionNumber != manifest.Version.Number)
        {
            throw new StrategyConfigException(
                $"rules.json \"version-number\" ({rules.VersionNumber}) must equal meta.json \"version.number\" ({manifest.Version.Number})."
            );
        }
    }

    private static bool VerifyHashAndRunnability(StrategyManifest manifest, string rulesJson)
    {
        var computed = ComputeRulesHash(rulesJson);
        var declared = manifest.Version.RulesHash;
        var matches = string.Equals(computed, declared, StringComparison.OrdinalIgnoreCase);

        // Active versions must be hash-verified; drafts may carry an unverified placeholder but are
        // never runnable (see the format doc §9.4).
        if (manifest.Version.Status == StrategyStatus.Active && !matches)
        {
            throw new StrategyConfigException(
                $"rules.json hash mismatch: declared {declared}, computed {computed}."
            );
        }

        return manifest.Version.Status == StrategyStatus.Active;
    }

    private static string ComputeRulesHash(string rulesJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rulesJson));
        return "sha256:" + Convert.ToHexStringLower(bytes);
    }

    // ---- indicators ------------------------------------------------------

    private static IReadOnlyList<IndicatorSpec> CompileIndicators(
        List<IndicatorDto?>? dtos,
        IReadOnlyDictionary<string, decimal> parameters
    )
    {
        var result = new List<IndicatorSpec>();
        if (dtos is null)
        {
            return result;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in dtos)
        {
            if (dto is null)
            {
                throw new StrategyConfigException("rules.json has a null indicator entry.");
            }

            RequireNonEmpty(dto.Id, "indicator \"id\"");
            if (!ids.Add(dto.Id!))
            {
                throw new StrategyConfigException($"Duplicate indicator id \"{dto.Id}\".");
            }

            if (string.IsNullOrWhiteSpace(dto.Kind) || !IndicatorFactory.IsKnown(dto.Kind))
            {
                throw new StrategyConfigException(
                    $"Indicator \"{dto.Id}\" has unknown kind \"{dto.Kind}\"."
                );
            }

            if (string.IsNullOrWhiteSpace(dto.Source) || !CandleFields.TryParse(dto.Source, out var source))
            {
                throw new StrategyConfigException(
                    $"Indicator \"{dto.Id}\" has unknown source \"{dto.Source}\"."
                );
            }

            var period = ResolvePeriod(dto, parameters);
            result.Add(new IndicatorSpec(dto.Id!, dto.Kind!, source, period));
        }

        return result;
    }

    private static int ResolvePeriod(IndicatorDto dto, IReadOnlyDictionary<string, decimal> parameters)
    {
        decimal value;
        switch (dto.Period.ValueKind)
        {
            case JsonValueKind.Number:
                value = dto.Period.GetDecimal();
                break;
            case JsonValueKind.String:
                var raw = dto.Period.GetString()!;
                value = ResolveParameterReference(raw, parameters, $"indicator \"{dto.Id}\" period");
                break;
            default:
                throw new StrategyConfigException(
                    $"Indicator \"{dto.Id}\" period must be a number or a parameter reference."
                );
        }

        if (!IsIntegral(value) || value < 1)
        {
            throw new StrategyConfigException(
                $"Indicator \"{dto.Id}\" period must resolve to a positive whole number (got {value})."
            );
        }

        return (int)value;
    }

    private static decimal ResolveParameterReference(
        string raw,
        IReadOnlyDictionary<string, decimal> parameters,
        string where
    )
    {
        if (!raw.StartsWith('$'))
        {
            throw new StrategyConfigException($"{where} string \"{raw}\" must be a \"$parameter\" reference.");
        }

        var name = raw[1..];
        if (!parameters.TryGetValue(name, out var value))
        {
            throw new StrategyConfigException($"{where} references undeclared parameter \"${name}\".");
        }

        return value;
    }

    // ---- rules (the condition DSL) ---------------------------------------

    private static IReadOnlyDictionary<string, ICondition> CompileRules(
        Dictionary<string, JsonElement>? dtos,
        IReadOnlyCollection<string> parameterNames,
        IReadOnlySet<string> indicatorIds
    )
    {
        if (dtos is null || dtos.Count == 0)
        {
            throw new StrategyConfigException("rules.json must define at least one rule.");
        }

        var compiled = new Dictionary<string, ICondition>(StringComparer.Ordinal);
        foreach (var (key, node) in dtos)
        {
            if (!RuleKeys.Contains(key))
            {
                throw new StrategyConfigException(
                    $"rules.json has an unknown rule \"{key}\" (expected entry-long|exit-long|entry-short|exit-short)."
                );
            }

            compiled[key] = CompileCondition(node, parameterNames, indicatorIds);
        }

        return compiled;
    }

    private static ICondition CompileCondition(
        JsonElement node,
        IReadOnlyCollection<string> parameterNames,
        IReadOnlySet<string> indicatorIds
    )
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            throw new StrategyConfigException("A condition must be a JSON object.");
        }

        var (op, value) = SingleProperty(node);
        switch (op)
        {
            case "all":
                return new AllCondition(CompileChildren(value, parameterNames, indicatorIds));
            case "any":
                return new AnyCondition(CompileChildren(value, parameterNames, indicatorIds));
            case "not":
                return new NotCondition(CompileCondition(value, parameterNames, indicatorIds));
            case "gt":
                return CompileComparison(ComparisonOperator.GreaterThan, value, parameterNames, indicatorIds);
            case "gte":
                return CompileComparison(ComparisonOperator.GreaterThanOrEqual, value, parameterNames, indicatorIds);
            case "lt":
                return CompileComparison(ComparisonOperator.LessThan, value, parameterNames, indicatorIds);
            case "lte":
                return CompileComparison(ComparisonOperator.LessThanOrEqual, value, parameterNames, indicatorIds);
            case "eq":
                return CompileComparison(ComparisonOperator.Equal, value, parameterNames, indicatorIds);
            case "crosses-above":
                return CompileCross(CrossDirection.Above, value, parameterNames, indicatorIds);
            case "crosses-below":
                return CompileCross(CrossDirection.Below, value, parameterNames, indicatorIds);
            default:
                throw new StrategyConfigException($"Unknown condition operator \"{op}\".");
        }
    }

    private static IReadOnlyList<ICondition> CompileChildren(
        JsonElement value,
        IReadOnlyCollection<string> parameterNames,
        IReadOnlySet<string> indicatorIds
    )
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new StrategyConfigException("\"all\"/\"any\" require an array of conditions.");
        }

        var children = new List<ICondition>(value.GetArrayLength());
        foreach (var child in value.EnumerateArray())
        {
            children.Add(CompileCondition(child, parameterNames, indicatorIds));
        }

        return children;
    }

    private static ComparisonCondition CompileComparison(
        ComparisonOperator op,
        JsonElement value,
        IReadOnlyCollection<string> parameterNames,
        IReadOnlySet<string> indicatorIds
    )
    {
        var (left, right) = OperandPair(value);
        return new ComparisonCondition(
            op,
            CompileOperand(left, parameterNames, indicatorIds),
            CompileOperand(right, parameterNames, indicatorIds)
        );
    }

    private static CrossCondition CompileCross(
        CrossDirection direction,
        JsonElement value,
        IReadOnlyCollection<string> parameterNames,
        IReadOnlySet<string> indicatorIds
    )
    {
        var (left, right) = OperandPair(value);
        return new CrossCondition(
            direction,
            CompileOperand(left, parameterNames, indicatorIds),
            CompileOperand(right, parameterNames, indicatorIds)
        );
    }

    private static IOperand CompileOperand(
        JsonElement element,
        IReadOnlyCollection<string> parameterNames,
        IReadOnlySet<string> indicatorIds
    )
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return new LiteralOperand(element.GetDecimal());

            case JsonValueKind.String:
                var token = element.GetString()!;
                if (token.StartsWith('$'))
                {
                    var name = token[1..];
                    RequireDeclaredParameter(name, parameterNames);
                    return new ParameterOperand(name);
                }

                RequireKnownSeries(token, indicatorIds);
                return new SeriesOperand(token, 0);

            case JsonValueKind.Object:
                return CompileSeriesObject(element, indicatorIds);

            default:
                throw new StrategyConfigException(
                    $"An operand must be a number, a series/parameter string, or a series object (got {element.ValueKind})."
                );
        }
    }

    private static SeriesOperand CompileSeriesObject(JsonElement element, IReadOnlySet<string> indicatorIds)
    {
        if (!element.TryGetProperty("series", out var seriesEl) || seriesEl.ValueKind != JsonValueKind.String)
        {
            throw new StrategyConfigException("A series operand object must have a string \"series\".");
        }

        var series = seriesEl.GetString()!;
        RequireKnownSeries(series, indicatorIds);

        var offset = 0;
        if (element.TryGetProperty("offset", out var offsetEl))
        {
            if (offsetEl.ValueKind != JsonValueKind.Number || !offsetEl.TryGetInt32(out offset))
            {
                throw new StrategyConfigException($"Series \"{series}\" has a non-integer \"offset\".");
            }

            if (offset < 0)
            {
                throw new StrategyConfigException(
                    $"Series \"{series}\" has a negative \"offset\" ({offset}); look-ahead is forbidden."
                );
            }
        }

        return new SeriesOperand(series, offset);
    }

    private static void RequireDeclaredParameter(string name, IReadOnlyCollection<string> parameterNames)
    {
        if (!parameterNames.Contains(name))
        {
            throw new StrategyConfigException($"Operand references undeclared parameter \"${name}\".");
        }
    }

    private static void RequireKnownSeries(string name, IReadOnlySet<string> indicatorIds)
    {
        if (!CandleFields.TryParse(name, out _) && !indicatorIds.Contains(name))
        {
            throw new StrategyConfigException(
                $"Operand references unknown series \"{name}\" (not a candle field or declared indicator)."
            );
        }
    }

    // ---- requested-risk & execution --------------------------------------

    private static RequestedRisk? BuildRequestedRisk(RequestedRiskDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        return new RequestedRisk(
            dto.StopLossPct ?? 0m,
            dto.TakeProfitPct ?? 0m,
            dto.MaxPositionPct ?? 0m
        );
    }

    private static ExecutionHints? BuildExecution(ExecutionDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        var orderType = dto.OrderType?.ToLowerInvariant() switch
        {
            "market" => OrderType.Market,
            "limit" => OrderType.Limit,
            null => OrderType.Market,
            _ => throw new StrategyConfigException(
                $"execution \"order-type\" is invalid: \"{dto.OrderType}\" (expected market|limit)."
            ),
        };

        var tif = dto.TimeInForce?.ToLowerInvariant() switch
        {
            "gtc" => TimeInForce.Gtc,
            "ioc" => TimeInForce.Ioc,
            "fok" => TimeInForce.Fok,
            null => TimeInForce.Gtc,
            _ => throw new StrategyConfigException(
                $"execution \"time-in-force\" is invalid: \"{dto.TimeInForce}\" (expected gtc|ioc|fok)."
            ),
        };

        return new ExecutionHints(orderType, tif, dto.SlippageTolerancePct ?? 0m);
    }

    // ---- shared helpers --------------------------------------------------

    private static (string Op, JsonElement Value) SingleProperty(JsonElement node)
    {
        string? op = null;
        JsonElement value = default;
        var count = 0;
        foreach (var property in node.EnumerateObject())
        {
            op = property.Name;
            value = property.Value;
            count++;
        }

        if (count != 1)
        {
            throw new StrategyConfigException(
                $"A condition node must have exactly one operator (found {count})."
            );
        }

        return (op!, value);
    }

    private static (JsonElement Left, JsonElement Right) OperandPair(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2)
        {
            throw new StrategyConfigException("A comparison/cross requires a two-element operand array.");
        }

        return (value[0], value[1]);
    }

    private static void RequireSchemaVersion(string? value, string what)
    {
        if (!string.Equals(value, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new StrategyConfigException(
                $"{what} \"schema-version\" must be \"{SupportedSchemaVersion}\" (got \"{value}\")."
            );
        }
    }

    private static void RequireNonEmpty(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StrategyConfigException($"{what} is required.");
        }
    }

    private static bool IsIntegral(decimal value) => value == Math.Truncate(value);

    // ---- DTOs ------------------------------------------------------------

    private sealed class MetaDto
    {
        public string? SchemaVersion { get; init; }
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? Author { get; init; }
        public bool? Deterministic { get; init; }
        public VersionDto? Version { get; init; }
        public ApplicabilityDto? Applicability { get; init; }
        public Dictionary<string, ParameterSpecDto>? Parameters { get; init; }
        public List<string>? Tags { get; init; }
    }

    private sealed class VersionDto
    {
        public int? Number { get; init; }
        public string? Status { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string? RulesHash { get; init; }
    }

    private sealed class ApplicabilityDto
    {
        public List<string>? Exchanges { get; init; }
        public List<string>? Symbols { get; init; }
        public string? Timeframe { get; init; }
        public int? WarmupBars { get; init; }
    }

    private sealed class ParameterSpecDto
    {
        public string? Type { get; init; }
        public decimal? Default { get; init; }
        public decimal? Min { get; init; }
        public decimal? Max { get; init; }
    }

    private sealed class RulesDto
    {
        public string? SchemaVersion { get; init; }
        public string? StrategyId { get; init; }
        public int VersionNumber { get; init; }
        public List<IndicatorDto?>? Indicators { get; init; }
        public Dictionary<string, JsonElement>? Rules { get; init; }
        public RequestedRiskDto? RequestedRisk { get; init; }
        public ExecutionDto? Execution { get; init; }
    }

    private sealed class IndicatorDto
    {
        public string? Id { get; init; }
        public string? Kind { get; init; }
        public string? Source { get; init; }
        public JsonElement Period { get; init; }
    }

    private sealed class RequestedRiskDto
    {
        public decimal? StopLossPct { get; init; }
        public decimal? TakeProfitPct { get; init; }
        public decimal? MaxPositionPct { get; init; }
    }

    private sealed class ExecutionDto
    {
        public string? OrderType { get; init; }
        public string? TimeInForce { get; init; }
        public decimal? SlippageTolerancePct { get; init; }
    }
}
