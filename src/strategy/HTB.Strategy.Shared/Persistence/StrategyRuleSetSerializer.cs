using System.Text.Json;
using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Persistence;

/// <summary>
/// Serializes a <see cref="StrategyRuleSet"/> to and from the JSON document stored in the
/// <c>rules</c> jsonb column. The <see cref="StrategyVersionId"/> is the row key and is supplied on
/// read, so only the rule body (direction, parameters, indicators, entry, exit, risk) is serialized.
/// Reading reconstructs through the domain constructors, so a stored document that violates a domain
/// invariant surfaces as a <see cref="StrategyDomainException"/> rather than a corrupt object.
/// </summary>
public static class StrategyRuleSetSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new OperandJsonConverter() },
    };

    /// <summary>Serializes the rule body (everything except the version id) to a JSON string.</summary>
    public static string Serialize(StrategyRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var body = new RuleSetBody(
            (int)rules.Direction,
            [.. rules.Parameters],
            [.. rules.Indicators],
            rules.Entry,
            rules.Exit,
            rules.Risk
        );

        return JsonSerializer.Serialize(body, Options);
    }

    /// <summary>
    /// Reconstructs a <see cref="StrategyRuleSet"/> from its stored <paramref name="json"/> body and
    /// the <paramref name="versionId"/> it belongs to.
    /// </summary>
    public static StrategyRuleSet Deserialize(StrategyVersionId versionId, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        RuleSetBody? body;
        try
        {
            body = JsonSerializer.Deserialize<RuleSetBody>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new StrategyDomainException($"rules JSON is invalid: {ex.Message}");
        }

        if (body is null)
        {
            throw new StrategyDomainException("rules JSON deserialized to a null body.");
        }

        return new StrategyRuleSet(
            versionId,
            (TradeDirection)body.Direction,
            body.Parameters,
            body.Indicators,
            body.Entry,
            body.Exit,
            body.Risk
        );
    }

    /// <summary>The JSON-facing shape of the rule body; the version id lives in the row key, not here.</summary>
    private sealed record RuleSetBody(
        int Direction,
        ParameterSpec[] Parameters,
        IndicatorSpec[] Indicators,
        SignalRule Entry,
        SignalRule Exit,
        RiskRules Risk
    );
}
