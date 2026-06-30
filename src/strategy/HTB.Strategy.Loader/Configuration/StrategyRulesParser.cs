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
                return ParseStringOperand(element.GetString(), context);
            default:
                throw new StrategyMetaException(
                    $"rules.json {context} must be a number or a string operand token."
                );
        }
    }

    private static Operand ParseStringOperand(string? token, string context)
    {
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
