using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Configuration;

/// <summary>
/// Parses and validates the <c>symbols.json</c> backfill manifest into
/// <see cref="SymbolLoadSpec"/>s. The format is an array of entries:
/// <code>
/// [
///   { "ticket": "BTCUSDT", "timeframes": ["M1", "M5"],
///     "date-range": { "from": "2020-01-01", "to": "2026-12-31" } }
/// ]
/// </code>
/// <c>to</c> is optional. Comments (<c>//</c>, <c>/* */</c>) and trailing commas are tolerated.
/// </summary>
public static class SymbolConfigParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads and parses the manifest at <paramref name="path"/>.</summary>
    public static async Task<IReadOnlyList<SymbolLoadSpec>> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return Parse(json);
    }

    /// <summary>Parses the manifest from a JSON string.</summary>
    public static IReadOnlyList<SymbolLoadSpec> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<Entry?>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<Entry?>>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new SymbolConfigException($"symbols.json is not valid JSON: {ex.Message}");
        }

        if (entries is null)
        {
            throw new SymbolConfigException("symbols.json must contain an array of entries.");
        }

        var specs = new List<SymbolLoadSpec>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            specs.Add(ToSpec(entries[i], i));
        }

        return specs;
    }

    private static SymbolLoadSpec ToSpec(Entry? entry, int index)
    {
        if (entry is null)
        {
            throw new SymbolConfigException($"Entry #{index} is null.");
        }

        if (string.IsNullOrWhiteSpace(entry.Ticket))
        {
            throw new SymbolConfigException($"Entry #{index} is missing a non-empty \"ticket\".");
        }

        if (entry.Timeframes is null || entry.Timeframes.Count == 0)
        {
            throw new SymbolConfigException(
                $"Entry \"{entry.Ticket}\" must list at least one timeframe."
            );
        }

        var timeframes = new List<Timeframe>(entry.Timeframes.Count);
        foreach (var raw in entry.Timeframes)
        {
            if (!Enum.TryParse<Timeframe>(raw, ignoreCase: true, out var tf) || !Enum.IsDefined(tf))
            {
                throw new SymbolConfigException(
                    $"Entry \"{entry.Ticket}\" has an unknown timeframe \"{raw}\"."
                );
            }

            timeframes.Add(tf);
        }

        if (entry.DateRange is null)
        {
            throw new SymbolConfigException(
                $"Entry \"{entry.Ticket}\" is missing a \"date-range\"."
            );
        }

        var from = ParseDate(entry.DateRange.From, entry.Ticket, "from");
        DateTimeOffset? to = entry.DateRange.To is null
            ? null
            : ParseDate(entry.DateRange.To, entry.Ticket, "to");

        if (to is { } upperBound && upperBound < from)
        {
            throw new SymbolConfigException(
                $"Entry \"{entry.Ticket}\" has a \"to\" date before its \"from\" date."
            );
        }

        return new SymbolLoadSpec(entry.Ticket, timeframes, from, to);
    }

    private static DateTimeOffset ParseDate(string? value, string ticket, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SymbolConfigException(
                $"Entry \"{ticket}\" is missing a \"{field}\" date in its \"date-range\"."
            );
        }

        if (
            !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed
            )
        )
        {
            throw new SymbolConfigException(
                $"Entry \"{ticket}\" has an invalid \"{field}\" date \"{value}\"."
            );
        }

        return parsed;
    }

    private sealed class Entry
    {
        [JsonPropertyName("ticket")]
        public string? Ticket { get; init; }

        [JsonPropertyName("timeframes")]
        public List<string?>? Timeframes { get; init; }

        [JsonPropertyName("date-range")]
        public DateRange? DateRange { get; init; }
    }

    private sealed class DateRange
    {
        [JsonPropertyName("from")]
        public string? From { get; init; }

        [JsonPropertyName("to")]
        public string? To { get; init; }
    }
}
