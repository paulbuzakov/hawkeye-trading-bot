using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;
using HTB.Strategy.Loader.Persistence;

namespace HTB.Strategy.Loader;

/// <summary>
/// The testable core of the strategy-loading CLI: resolves each path to a strategy package (a
/// <c>.htbstrat</c>/zip file) or a pair directory (containing <c>meta.json</c> + <c>rules.json</c>),
/// runs the non-throwing <see cref="IStrategyLoader.Validate(string, string)"/> check step, writes a
/// verdict per input, and — when <c>--save</c> is passed and a store is wired — persists each valid
/// strategy into the registry. It never throws on a bad strategy: every failure becomes a report line
/// and a non-zero exit code, so a registry/CI can admit or reject in batch.
/// </summary>
public sealed class StrategyLoader(
    IStrategyPackageLoader packageLoader,
    IStrategyLoader pairLoader,
    TextWriter output,
    TextWriter error,
    IStrategyStore? store = null
)
{
    /// <summary>All inputs loaded and are valid.</summary>
    public const int ExitOk = 0;

    /// <summary>The command was misused (no paths, or <c>--save</c> without a connection).</summary>
    public const int ExitUsage = 1;

    /// <summary>At least one input failed to load (invalid strategy, or path not found).</summary>
    public const int ExitInvalid = 2;

    private const string SaveFlag = "--save";
    private const string MetaFileName = "meta.json";
    private const string RulesFileName = "rules.json";

    private readonly IStrategyPackageLoader _packageLoader = packageLoader;
    private readonly IStrategyLoader _pairLoader = pairLoader;
    private readonly TextWriter _output = output;
    private readonly TextWriter _error = error;
    private readonly IStrategyStore? _store = store;

    /// <summary>Checks (and optionally saves) every path argument and returns the process exit code.</summary>
    public async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(args);

        var save = args.Contains(SaveFlag);
        var paths = args.Where(arg => arg != SaveFlag).ToList();

        if (paths.Count == 0)
        {
            _error.WriteLine(
                $"usage: htb-strategy [{SaveFlag}] <strategy.htbstrat | strategy-dir> [more...]"
            );
            return ExitUsage;
        }

        if (save && _store is null)
        {
            _error.WriteLine(
                $"{SaveFlag} requires a database connection (set HTB_CONNECTION_STRING)."
            );
            return ExitUsage;
        }

        var allValid = true;
        foreach (var path in paths)
        {
            var loaded = Check(path);
            Report(path, loaded.Result);

            if (loaded.Result.IsValid && save)
            {
                var record = await _store!.SaveAsync(
                    loaded.Result.Definition!,
                    loaded.ManifestJson!,
                    loaded.RulesJson!,
                    cancellationToken
                );
                _output.WriteLine(
                    $"     saved {record.StrategyId} v{record.VersionNumber} to registry"
                );
            }

            allValid &= loaded.Result.IsValid;
        }

        return allValid ? ExitOk : ExitInvalid;
    }

    private LoadedStrategy Check(string path)
    {
        // A file is a package archive; a directory is a loose meta.json + rules.json pair.
        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var documents = _packageLoader.ReadDocuments(stream);
                var result = _pairLoader.Validate(documents.ManifestJson, documents.RulesJson);
                return new LoadedStrategy(result, documents.ManifestJson, documents.RulesJson);
            }
            catch (Shared.Strategy.Strategy.StrategyConfigException ex)
            {
                return new LoadedStrategy(StrategyValidationResult.Invalid(ex.Message), null, null);
            }
        }

        if (Directory.Exists(path))
        {
            var metaPath = Path.Combine(path, MetaFileName);
            var rulesPath = Path.Combine(path, RulesFileName);
            if (!File.Exists(metaPath) || !File.Exists(rulesPath))
            {
                return new LoadedStrategy(
                    StrategyValidationResult.Invalid(
                        $"directory must contain both {MetaFileName} and {RulesFileName}."
                    ),
                    null,
                    null
                );
            }

            var manifestJson = File.ReadAllText(metaPath);
            var rulesJson = File.ReadAllText(rulesPath);
            return new LoadedStrategy(
                _pairLoader.Validate(manifestJson, rulesJson),
                manifestJson,
                rulesJson
            );
        }

        return new LoadedStrategy(
            StrategyValidationResult.Invalid("no such file or directory."),
            null,
            null
        );
    }

    private void Report(string path, StrategyValidationResult result)
    {
        if (result.IsValid)
        {
            var manifest = result.Definition!.Manifest;
            var runnable = result.IsRunnable ? "runnable" : "not runnable (draft)";
            _output.WriteLine(
                $"OK   {path}: {manifest.Id} v{manifest.Version.Number} "
                    + $"({manifest.Version.Status.ToString().ToLowerInvariant()}, {runnable})"
            );
            return;
        }

        _output.WriteLine($"FAIL {path}:");
        foreach (var failure in result.Errors)
        {
            _output.WriteLine($"       {failure}");
        }
    }

    private sealed record LoadedStrategy(
        StrategyValidationResult Result,
        string? ManifestJson,
        string? RulesJson
    );
}
