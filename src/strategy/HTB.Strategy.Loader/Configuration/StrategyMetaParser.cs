using System.Text.Json;
using System.Text.Json.Serialization;
using HTB.MarketData.Shared.Domain;
using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Parses and validates a strategy bundle's <c>meta.json</c> into a <see cref="StrategyDefinition"/>.
/// The format mirrors <c>docs/strategies/&lt;id&gt;/meta.json</c>:
/// <code>
/// { "id": "rsi-movement", "version": 1, "name": "RSI Movement", "description": "...",
///   "tags": ["mean-reversion"], "exchanges": ["binance"], "symbols": ["BTCUSDT"],
///   "timeframes": ["H1", "H4"], "warmup-bars": 200 }
/// </code>
/// Comments (<c>//</c>, <c>/* */</c>) and trailing commas are tolerated.
/// </summary>
public static class StrategyMetaParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads and parses the <c>meta.json</c> at <paramref name="path"/>.</summary>
    public static async Task<StrategyDefinition> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return Parse(json);
    }

    /// <summary>Parses a <c>meta.json</c> document from a JSON string.</summary>
    public static StrategyDefinition Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Meta? meta;
        try
        {
            meta = JsonSerializer.Deserialize<Meta>(json, _options);
        }
        catch (JsonException ex)
        {
            throw new StrategyMetaException($"meta.json is not valid JSON: {ex.Message}");
        }

        if (meta is null)
        {
            throw new StrategyMetaException("meta.json must contain a strategy object.");
        }

        if (string.IsNullOrWhiteSpace(meta.Id))
        {
            throw new StrategyMetaException("meta.json is missing a non-empty \"id\".");
        }

        if (meta.Version is not { } version)
        {
            throw new StrategyMetaException($"Strategy \"{meta.Id}\" is missing a \"version\".");
        }

        if (version < 1)
        {
            throw new StrategyMetaException($"Strategy \"{meta.Id}\" has a non-positive \"version\" ({version}).");
        }

        if (string.IsNullOrWhiteSpace(meta.Name))
        {
            throw new StrategyMetaException($"Strategy \"{meta.Id}\" is missing a non-empty \"name\".");
        }

        if (meta.WarmupBars is < 0)
        {
            throw new StrategyMetaException($"Strategy \"{meta.Id}\" has a negative \"warmup-bars\".");
        }

        return new StrategyDefinition
        {
            Id = new StrategyId(meta.Id),
            Version = new StrategyVersion(version),
            Name = meta.Name,
            Description = meta.Description ?? string.Empty,
            Tags = ParseTags(meta.Tags, meta.Id),
            Exchanges = ParseCodes(meta.Exchanges, meta.Id, "exchanges", value => new ExchangeCode(value)),
            Symbols = ParseCodes(meta.Symbols, meta.Id, "symbols", value => new SymbolCode(value)),
            Timeframes = ParseTimeframes(meta.Timeframes, meta.Id),
            WarmupBars = meta.WarmupBars ?? 0,
        };
    }

    private static IReadOnlyList<string> ParseTags(List<string?>? raw, string id)
    {
        if (raw is null)
        {
            return [];
        }

        var tags = new List<string>(raw.Count);
        foreach (var value in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new StrategyMetaException($"Strategy \"{id}\" has an empty tag.");
            }

            tags.Add(value);
        }

        return tags;
    }

    private static IReadOnlyList<T> ParseCodes<T>(List<string?>? raw, string id, string field, Func<string, T> factory)
    {
        if (raw is null || raw.Count == 0)
        {
            throw new StrategyMetaException($"Strategy \"{id}\" must list at least one \"{field}\" entry.");
        }

        var codes = new List<T>(raw.Count);
        foreach (var value in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new StrategyMetaException($"Strategy \"{id}\" has an empty \"{field}\" entry.");
            }

            codes.Add(factory(value));
        }

        return codes;
    }

    private static IReadOnlyList<Timeframe> ParseTimeframes(List<string?>? raw, string id)
    {
        if (raw is null || raw.Count == 0)
        {
            throw new StrategyMetaException($"Strategy \"{id}\" must list at least one \"timeframes\" entry.");
        }

        var timeframes = new List<Timeframe>(raw.Count);
        foreach (var value in raw)
        {
            if (
                string.IsNullOrWhiteSpace(value)
                || !Enum.TryParse<Timeframe>(value, ignoreCase: true, out var tf)
                || !Enum.IsDefined(tf)
            )
            {
                throw new StrategyMetaException($"Strategy \"{id}\" has an unknown timeframe \"{value}\".");
            }

            timeframes.Add(tf);
        }

        return timeframes;
    }

    private sealed class Meta
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("version")]
        public int? Version { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("tags")]
        public List<string?>? Tags { get; init; }

        [JsonPropertyName("exchanges")]
        public List<string?>? Exchanges { get; init; }

        [JsonPropertyName("symbols")]
        public List<string?>? Symbols { get; init; }

        [JsonPropertyName("timeframes")]
        public List<string?>? Timeframes { get; init; }

        [JsonPropertyName("warmup-bars")]
        public int? WarmupBars { get; init; }
    }
}
