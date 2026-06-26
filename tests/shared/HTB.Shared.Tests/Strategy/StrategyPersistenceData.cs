using System.Security.Cryptography;
using System.Text;
using HTB.Shared.Strategy.Domain;
using SharedLoader = HTB.Shared.Strategy.Strategy.StrategyLoader;

namespace HTB.Shared.Tests.Strategy;

/// <summary>
/// Builders for strategy-registry persistence tests: raw <c>meta.json</c>/<c>rules.json</c> text with
/// a controllable id/version/status/hash, plus the loaded <see cref="StrategyDefinition"/> they parse
/// into. The <c>rules.json</c> threshold knob varies the bytes (and therefore the real hash) so tests
/// can exercise "same bytes" vs "different bytes" under one version.
/// </summary>
internal static class StrategyPersistenceData
{
    private static readonly SharedLoader Loader = new();

    public static string Rules(string id, int version, int threshold = 0) => $$"""
        {
          "schema-version": "1.0",
          "strategy-id": "{{id}}",
          "version-number": {{version}},
          "indicators": [],
          "rules": { "entry-long": { "gt": ["close", {{threshold}}] } }
        }
        """;

    public static string Meta(string id, int version, string status, string rulesHash) => $$"""
        {
          "schema-version": "1.0",
          "id": "{{id}}",
          "name": "Demo {{id}}",
          "deterministic": true,
          "version": { "number": {{version}}, "status": "{{status}}", "created-at": "2026-01-01T00:00:00Z", "rules-hash": "{{rulesHash}}" },
          "applicability": { "timeframe": "H1", "warmup-bars": 0 },
          "parameters": {}
        }
        """;

    public static string HashOf(string rulesJson) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rulesJson)));

    /// <summary>A loaded, active (runnable) version whose declared hash matches its rules bytes.</summary>
    public static Loaded Active(string id, int version, int threshold = 0)
    {
        var rules = Rules(id, version, threshold);
        var meta = Meta(id, version, "active", HashOf(rules));
        return new Loaded(Loader.Load(meta, rules), meta, rules);
    }

    /// <summary>A loaded draft version carrying the supplied (unverified) declared hash.</summary>
    public static Loaded Draft(string id, int version, string declaredHash)
    {
        var rules = Rules(id, version);
        var meta = Meta(id, version, "draft", declaredHash);
        return new Loaded(Loader.Load(meta, rules), meta, rules);
    }

    internal sealed record Loaded(StrategyDefinition Definition, string MetaJson, string RulesJson);
}
