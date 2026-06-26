using System.IO.Compression;
using System.Text;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Strategy;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class StrategyPackageLoaderTests
{
    private const int MaxMetaBytes = 64 * 1024;
    private const int MaxRulesBytes = 1024 * 1024;

    private static readonly StrategyPackageLoader Loader = new(new StrategyLoader());

    // ---- helpers ---------------------------------------------------------

    /// <summary>Builds an in-memory zip from the given (name, bytes) entries, rewound for reading.</summary>
    private static MemoryStream Package(params (string Name, byte[] Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var stream = entry.Open();
                stream.Write(content, 0, content.Length);
            }
        }

        ms.Position = 0;
        return ms;
    }

    private static (string Meta, string Rules) ValidPair()
    {
        var rulesJson = Json(ValidRules());
        var metaJson = Json(ValidMeta());
        return (metaJson, rulesJson);
    }

    private static MemoryStream ValidPackage()
    {
        var (meta, rules) = ValidPair();
        return Package(
            ("meta.json", Encoding.UTF8.GetBytes(meta)),
            ("rules.json", Encoding.UTF8.GetBytes(rules))
        );
    }

    private static StrategyConfigException AssertInvalid(Stream package) =>
        Assert.Throws<StrategyConfigException>(() => Loader.Load(package));

    // ---- happy paths -----------------------------------------------------

    [Fact]
    public void Loads_a_valid_package_into_the_same_definition_as_the_inner_loader()
    {
        var (meta, rules) = ValidPair();
        var direct = new StrategyLoader().Load(meta, rules);

        using var package = ValidPackage();
        var def = Loader.Load(package);

        Assert.Equal(direct.Manifest.Id, def.Manifest.Id);
        Assert.Equal(direct.Manifest.Version.Number, def.Manifest.Version.Number);
        Assert.Equal(direct.IsRunnable, def.IsRunnable);
        Assert.Equal(direct.Rules.Indicators.Count, def.Rules.Indicators.Count);
    }

    [Fact]
    public void ReadDocuments_returns_the_exact_entry_text()
    {
        var (meta, rules) = ValidPair();

        using var package = ValidPackage();
        var documents = Loader.ReadDocuments(package);

        Assert.Equal(meta, documents.ManifestJson);
        Assert.Equal(rules, documents.RulesJson);
    }

    [Fact]
    public void Passes_the_exact_entry_text_through_to_the_inner_loader()
    {
        var (meta, rules) = ValidPair();
        var capturing = new CapturingLoader();
        var loader = new StrategyPackageLoader(capturing);

        using var package = ValidPackage();
        loader.Load(package);

        Assert.Equal(meta, capturing.Meta);
        Assert.Equal(rules, capturing.Rules);
    }

    [Fact]
    public void Ignores_unknown_extra_entries()
    {
        var (meta, rules) = ValidPair();
        using var package = Package(
            ("README.md", Encoding.UTF8.GetBytes("# strategy")),
            ("meta.json", Encoding.UTF8.GetBytes(meta)),
            ("rules.json", Encoding.UTF8.GetBytes(rules))
        );

        var def = Loader.Load(package);

        Assert.Equal("rsi-movement", def.Manifest.Id);
    }

    // ---- guard & archive failures ----------------------------------------

    [Fact]
    public void Null_package_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => Loader.Load(null!));
    }

    [Fact]
    public void A_stream_that_is_not_a_zip_is_rejected()
    {
        using var notAZip = new MemoryStream([1, 2, 3, 4, 5]);
        var ex = AssertInvalid(notAZip);
        Assert.Contains("not a valid zip", ex.Message);
    }

    [Fact]
    public void Missing_meta_entry_is_rejected()
    {
        var (_, rules) = ValidPair();
        using var package = Package(("rules.json", Encoding.UTF8.GetBytes(rules)));
        Assert.Contains("meta.json", AssertInvalid(package).Message);
    }

    [Fact]
    public void Missing_rules_entry_is_rejected()
    {
        var (meta, _) = ValidPair();
        using var package = Package(("meta.json", Encoding.UTF8.GetBytes(meta)));
        Assert.Contains("rules.json", AssertInvalid(package).Message);
    }

    [Fact]
    public void Duplicate_meta_entry_is_rejected()
    {
        var (meta, rules) = ValidPair();
        using var package = Package(
            ("meta.json", Encoding.UTF8.GetBytes(meta)),
            ("meta.json", Encoding.UTF8.GetBytes(meta)),
            ("rules.json", Encoding.UTF8.GetBytes(rules))
        );
        Assert.Contains("more than one", AssertInvalid(package).Message);
    }

    [Fact]
    public void Duplicate_rules_entry_is_rejected()
    {
        var (meta, rules) = ValidPair();
        using var package = Package(
            ("meta.json", Encoding.UTF8.GetBytes(meta)),
            ("rules.json", Encoding.UTF8.GetBytes(rules)),
            ("rules.json", Encoding.UTF8.GetBytes(rules))
        );
        Assert.Contains("more than one", AssertInvalid(package).Message);
    }

    [Fact]
    public void Oversized_meta_entry_is_rejected()
    {
        var (_, rules) = ValidPair();
        var bomb = Enumerable.Repeat((byte)0x41, MaxMetaBytes + 1).ToArray();
        using var package = Package(
            ("meta.json", bomb),
            ("rules.json", Encoding.UTF8.GetBytes(rules))
        );
        Assert.Contains("exceeds", AssertInvalid(package).Message);
    }

    [Fact]
    public void Oversized_rules_entry_is_rejected()
    {
        var (meta, _) = ValidPair();
        var bomb = Enumerable.Repeat((byte)0x41, MaxRulesBytes + 1).ToArray();
        using var package = Package(
            ("meta.json", Encoding.UTF8.GetBytes(meta)),
            ("rules.json", bomb)
        );
        Assert.Contains("exceeds", AssertInvalid(package).Message);
    }

    [Fact]
    public void An_entry_that_is_not_valid_utf8_is_rejected()
    {
        var (_, rules) = ValidPair();
        using var package = Package(
            ("meta.json", [0xFF, 0xFE, 0xFD]),
            ("rules.json", Encoding.UTF8.GetBytes(rules))
        );
        Assert.Contains("UTF-8", AssertInvalid(package).Message);
    }

    // ---- Validate (non-throwing check step) ------------------------------

    [Fact]
    public void Validate_returns_a_valid_verdict_for_a_good_package()
    {
        using var package = ValidPackage();
        var result = Loader.Validate(package);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Definition);
        Assert.Empty(result.Errors);
        Assert.Equal("rsi-movement", result.Definition!.Manifest.Id);
    }

    [Fact]
    public void Validate_returns_an_invalid_verdict_instead_of_throwing()
    {
        using var notAZip = new MemoryStream([1, 2, 3, 4, 5]);
        var result = Loader.Validate(notAZip);

        Assert.False(result.IsValid);
        Assert.Null(result.Definition);
        Assert.False(result.IsRunnable);
        var error = Assert.Single(result.Errors);
        Assert.Contains("not a valid zip", error);
    }

    [Fact]
    public void Validate_null_package_still_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => Loader.Validate(null!));
    }

    private sealed class CapturingLoader : IStrategyLoader
    {
        private readonly StrategyLoader _real = new();

        public string? Meta { get; private set; }

        public string? Rules { get; private set; }

        public StrategyDefinition Load(string manifestJson, string rulesJson)
        {
            Meta = manifestJson;
            Rules = rulesJson;
            return _real.Load(manifestJson, rulesJson);
        }

        public StrategyValidationResult Validate(string manifestJson, string rulesJson) =>
            _real.Validate(manifestJson, rulesJson);
    }
}
