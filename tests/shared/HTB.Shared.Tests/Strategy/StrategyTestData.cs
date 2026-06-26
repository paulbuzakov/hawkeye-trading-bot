using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HTB.Shared.MarketData.Domain;

namespace HTB.Shared.Tests.Strategy;

/// <summary>
/// Shared builders for the strategy tests: a candle factory and mutable JSON object graphs (with
/// valid kebab-case keys) for <c>meta.json</c> / <c>rules.json</c> that individual tests tweak.
/// </summary>
internal static class StrategyTestData
{
    /// <summary>A candle whose OHLC are all <paramref name="close"/> (volume optional).</summary>
    public static Candle Candle(decimal close, decimal volume = 0m) =>
        new()
        {
            ExchangeId = 1,
            SymbolId = 1,
            Interval = Timeframe.H1,
            OpenTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Open = close,
            High = close,
            Low = close,
            Close = close,
            Volume = volume,
            QuoteVolume = 0m,
            TradeCount = 0,
            IsClosed = true,
            IngestedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    /// <summary>A window of candles whose closes are the supplied values.</summary>
    public static IReadOnlyList<Candle> Closes(params decimal[] closes) =>
        closes.Select(c => Candle(c)).ToList();

    public static string Json(object graph) => JsonSerializer.Serialize(graph);

    /// <summary>The same <c>sha256:&lt;hex&gt;</c> the loader computes for a rules document.</summary>
    public static string HashOf(string rulesJson) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rulesJson)));

    /// <summary>A complete, valid <c>meta.json</c> object graph (draft, unverified hash).</summary>
    public static Dictionary<string, object?> ValidMeta() =>
        new()
        {
            ["schema-version"] = "1.0",
            ["id"] = "rsi-movement",
            ["name"] = "RSI Movement",
            ["description"] = "Long-only mean reversion.",
            ["author"] = "paul",
            ["deterministic"] = true,
            ["version"] = new Dictionary<string, object?>
            {
                ["number"] = 1,
                ["status"] = "draft",
                ["created-at"] = "2026-06-26T00:00:00Z",
                ["rules-hash"] = "sha256:" + new string('0', 64),
            },
            ["applicability"] = new Dictionary<string, object?>
            {
                ["exchanges"] = new[] { "binance" },
                ["symbols"] = new[] { "BTCUSDT" },
                ["timeframe"] = "H1",
                ["warmup-bars"] = 200,
            },
            ["parameters"] = new Dictionary<string, object?>
            {
                ["rsi-period"] = Param("int", 14, 2, 50),
                ["rsi-oversold"] = Param("decimal", 30, 5, 45),
                ["rsi-overbought"] = Param("decimal", 70, 55, 95),
                ["ema-period"] = Param("int", 200, 20, 400),
            },
            ["tags"] = new[] { "rsi" },
        };

    /// <summary>A complete, valid <c>rules.json</c> object graph matching <see cref="ValidMeta"/>.</summary>
    public static Dictionary<string, object?> ValidRules() =>
        new()
        {
            ["schema-version"] = "1.0",
            ["strategy-id"] = "rsi-movement",
            ["version-number"] = 1,
            ["indicators"] = new object[]
            {
                Indicator("rsi", "rsi", "close", "$rsi-period"),
                Indicator("ema-slow", "ema", "close", "$ema-period"),
            },
            ["rules"] = new Dictionary<string, object?>
            {
                ["entry-long"] = new Dictionary<string, object?>
                {
                    ["all"] = new object[]
                    {
                        Cmp("lt", "rsi", "$rsi-oversold"),
                        Cmp("gt", "close", "ema-slow"),
                    },
                },
                ["exit-long"] = new Dictionary<string, object?>
                {
                    ["any"] = new object[]
                    {
                        Cmp("gt", "rsi", "$rsi-overbought"),
                        new Dictionary<string, object?> { ["crosses-below"] = new object[] { "close", "ema-slow" } },
                    },
                },
            },
            ["requested-risk"] = new Dictionary<string, object?>
            {
                ["stop-loss-pct"] = 2.0,
                ["take-profit-pct"] = 4.0,
                ["max-position-pct"] = 10.0,
            },
            ["execution"] = new Dictionary<string, object?>
            {
                ["order-type"] = "market",
                ["time-in-force"] = "gtc",
                ["slippage-tolerance-pct"] = 0.1,
            },
        };

    public static Dictionary<string, object?> Param(string type, object def, object min, object max) =>
        new()
        {
            ["type"] = type,
            ["default"] = def,
            ["min"] = min,
            ["max"] = max,
        };

    public static Dictionary<string, object?> Indicator(string id, string kind, string source, object period) =>
        new()
        {
            ["id"] = id,
            ["kind"] = kind,
            ["source"] = source,
            ["period"] = period,
        };

    public static Dictionary<string, object?> Cmp(string op, object left, object right) =>
        new() { [op] = new[] { left, right } };
}
