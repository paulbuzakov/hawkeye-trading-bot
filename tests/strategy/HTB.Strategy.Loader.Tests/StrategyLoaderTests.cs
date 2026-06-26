using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Persistence;
using HTB.Strategy.Loader;
using HTB.Strategy.Loader.Persistence;

namespace HTB.Strategy.Loader.Tests;

public sealed class StrategyLoaderTests : IDisposable
{
    private const string ZeroHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    private static readonly string RulesJson = """
        {
          "schema-version": "1.0",
          "strategy-id": "demo",
          "version-number": 1,
          "indicators": [],
          "rules": { "entry-long": { "gt": ["close", 0] } }
        }
        """;

    private readonly List<string> _tempPaths = [];
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();
    private readonly Shared.Strategy.Strategy.StrategyLoader _json = new();
    private readonly StrategyLoader _runner;

    public StrategyLoaderTests()
    {
        _runner = NewRunner();
    }

    private StrategyLoader NewRunner(IStrategyStore? store = null) =>
        new(new Shared.Strategy.Strategy.StrategyPackageLoader(_json), _json, _out, _err, store);

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    // ---- guards & usage --------------------------------------------------

    [Fact]
    public async Task RunAsync_null_args_throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _runner.RunAsync(null!));
    }

    [Fact]
    public async Task RunAsync_with_no_paths_prints_usage_and_returns_usage_code()
    {
        var code = await _runner.RunAsync([]);

        Assert.Equal(StrategyLoader.ExitUsage, code);
        Assert.Contains("usage:", _err.ToString());
        Assert.Empty(_out.ToString());
    }

    [Fact]
    public async Task RunAsync_with_only_the_save_flag_is_a_usage_error()
    {
        var code = await NewRunner(new RecordingStore()).RunAsync([SaveArg]);

        Assert.Equal(StrategyLoader.ExitUsage, code);
        Assert.Contains("usage:", _err.ToString());
    }

    // ---- packages --------------------------------------------------------

    [Fact]
    public async Task Valid_draft_package_reports_ok_but_not_runnable()
    {
        var package = PackageFile(MetaJson("draft", ZeroHash), RulesJson);

        var code = await _runner.RunAsync([package]);

        Assert.Equal(StrategyLoader.ExitOk, code);
        Assert.Contains("OK", _out.ToString());
        Assert.Contains("demo v1", _out.ToString());
        Assert.Contains("not runnable (draft)", _out.ToString());
    }

    [Fact]
    public async Task Valid_active_hash_verified_package_reports_runnable()
    {
        var package = PackageFile(MetaJson("active", HashOf(RulesJson)), RulesJson);

        var code = await _runner.RunAsync([package]);

        Assert.Equal(StrategyLoader.ExitOk, code);
        Assert.Contains("(active, runnable)", _out.ToString());
    }

    [Fact]
    public async Task Invalid_package_reports_fail_and_returns_invalid_code()
    {
        var notAZip = NewTempFile(".htbstrat");
        File.WriteAllBytes(notAZip, [1, 2, 3, 4, 5]);

        var code = await _runner.RunAsync([notAZip]);

        Assert.Equal(StrategyLoader.ExitInvalid, code);
        Assert.Contains("FAIL", _out.ToString());
        Assert.Contains("not a valid zip", _out.ToString());
    }

    // ---- pair directories ------------------------------------------------

    [Fact]
    public async Task Valid_pair_directory_reports_ok()
    {
        var dir = PairDir(MetaJson("draft", ZeroHash), RulesJson);

        var code = await _runner.RunAsync([dir]);

        Assert.Equal(StrategyLoader.ExitOk, code);
        Assert.Contains("demo v1", _out.ToString());
    }

    [Fact]
    public async Task Pair_directory_missing_rules_is_invalid()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "meta.json"), MetaJson("draft", ZeroHash));

        var code = await _runner.RunAsync([dir]);

        Assert.Equal(StrategyLoader.ExitInvalid, code);
        Assert.Contains("must contain both meta.json and rules.json", _out.ToString());
    }

    [Fact]
    public async Task Pair_directory_missing_meta_is_invalid()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "rules.json"), RulesJson);

        var code = await _runner.RunAsync([dir]);

        Assert.Equal(StrategyLoader.ExitInvalid, code);
        Assert.Contains("must contain both", _out.ToString());
    }

    // ---- resolution & batch ----------------------------------------------

    [Fact]
    public async Task Nonexistent_path_is_invalid()
    {
        var code = await _runner.RunAsync(
            [Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}")]
        );

        Assert.Equal(StrategyLoader.ExitInvalid, code);
        Assert.Contains("no such file or directory", _out.ToString());
    }

    [Fact]
    public async Task Mixed_batch_reports_each_and_returns_invalid_when_any_fails()
    {
        var good = PairDir(MetaJson("draft", ZeroHash), RulesJson);
        var bad = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        var code = await _runner.RunAsync([good, bad]);

        Assert.Equal(StrategyLoader.ExitInvalid, code);
        var text = _out.ToString();
        Assert.Contains("OK", text);
        Assert.Contains("FAIL", text);
    }

    // ---- --save ----------------------------------------------------------

    [Fact]
    public async Task Save_flag_without_a_store_is_a_usage_error()
    {
        var dir = PairDir(MetaJson("draft", ZeroHash), RulesJson);

        var code = await _runner.RunAsync([SaveArg, dir]);

        Assert.Equal(StrategyLoader.ExitUsage, code);
        Assert.Contains("requires a database connection", _err.ToString());
    }

    [Fact]
    public async Task Save_persists_each_valid_strategy_and_reports_it()
    {
        var store = new RecordingStore();
        var dir = PairDir(MetaJson("draft", ZeroHash), RulesJson);

        var code = await NewRunner(store).RunAsync([SaveArg, dir]);

        Assert.Equal(StrategyLoader.ExitOk, code);
        var saved = Assert.Single(store.Saved);
        Assert.Equal("demo", saved.StrategyId);
        Assert.Equal(1, saved.VersionNumber);
        Assert.Contains("saved demo v1 to registry", _out.ToString());
    }

    [Fact]
    public async Task Save_does_not_persist_invalid_inputs()
    {
        var store = new RecordingStore();
        var bad = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        var code = await NewRunner(store).RunAsync([SaveArg, bad]);

        Assert.Equal(StrategyLoader.ExitInvalid, code);
        Assert.Empty(store.Saved);
    }

    // ---- helpers ---------------------------------------------------------

    private const string SaveArg = "--save";

    private static string MetaJson(string status, string rulesHash) => $$"""
        {
          "schema-version": "1.0",
          "id": "demo",
          "name": "Demo Strategy",
          "deterministic": true,
          "version": { "number": 1, "status": "{{status}}", "created-at": "2026-01-01T00:00:00Z", "rules-hash": "{{rulesHash}}" },
          "applicability": { "timeframe": "H1", "warmup-bars": 0 },
          "parameters": {}
        }
        """;

    private static string HashOf(string rulesJson) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rulesJson)));

    private string PackageFile(string meta, string rules)
    {
        var path = NewTempFile(".htbstrat");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "meta.json", meta);
        WriteEntry(archive, "rules.json", rules);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using var stream = archive.CreateEntry(name).Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private string PairDir(string meta, string rules)
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "meta.json"), meta);
        File.WriteAllText(Path.Combine(dir, "rules.json"), rules);
        return dir;
    }

    private string NewTempFile(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"htb-strat-{Guid.NewGuid():N}{extension}");
        _tempPaths.Add(path);
        return path;
    }

    private string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"htb-strat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempPaths.Add(path);
        return path;
    }

    private sealed class RecordingStore : IStrategyStore
    {
        public List<StrategyVersionRecord> Saved { get; } = [];

        public Task<StrategyVersionRecord> SaveAsync(
            StrategyDefinition definition,
            string manifestJson,
            string rulesJson,
            CancellationToken cancellationToken = default
        )
        {
            var record = new StrategyVersionRecord
            {
                StrategyId = definition.Manifest.Id,
                VersionNumber = definition.Manifest.Version.Number,
            };
            Saved.Add(record);
            return Task.FromResult(record);
        }
    }
}
