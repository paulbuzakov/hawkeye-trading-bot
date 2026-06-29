namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Parsed command-line arguments for the strategy loader. Currently a single required flag:
/// <c>--meta &lt;path&gt;</c>, pointing at a strategy bundle's <c>meta.json</c>.
/// </summary>
public sealed record StrategyLoaderArgs(string MetaPath)
{
    public const string MetaFlag = "--meta";

    public static StrategyLoaderArgs Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? metaPath = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != MetaFlag)
            {
                throw new StrategyMetaException($"Unknown argument \"{args[i]}\". Usage: {MetaFlag} <path>.");
            }

            if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                throw new StrategyMetaException($"{MetaFlag} requires a file path argument.");
            }

            metaPath = args[i + 1];
            i++;
        }

        if (metaPath is null)
        {
            throw new StrategyMetaException($"Missing required argument {MetaFlag} <path>.");
        }

        return new StrategyLoaderArgs(metaPath);
    }
}
