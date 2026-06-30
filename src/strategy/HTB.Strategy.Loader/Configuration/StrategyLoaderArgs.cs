namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Parsed command-line arguments for the strategy loader. Two required flags:
/// <c>--meta &lt;path&gt;</c> pointing at a bundle's <c>meta.json</c>, and
/// <c>--rules &lt;path&gt;</c> pointing at its <c>rules.json</c>. Flags may appear in either
/// order; each must appear exactly once.
/// </summary>
public sealed record StrategyLoaderArgs(string MetaPath, string RulesPath)
{
    public const string MetaFlag = "--meta";
    public const string RulesFlag = "--rules";

    public static StrategyLoaderArgs Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? metaPath = null;
        string? rulesPath = null;

        for (var i = 0; i < args.Count; i++)
        {
            var flag = args[i];
            if (flag != MetaFlag && flag != RulesFlag)
            {
                throw new StrategyMetaException(
                    $"Unknown argument \"{flag}\". Usage: {MetaFlag} <path> {RulesFlag} <path>."
                );
            }

            if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                throw new StrategyMetaException($"{flag} requires a file path argument.");
            }

            var value = args[i + 1];
            i++;

            if (flag == MetaFlag)
            {
                metaPath = AssignOnce(metaPath, value, MetaFlag);
            }
            else
            {
                rulesPath = AssignOnce(rulesPath, value, RulesFlag);
            }
        }

        if (metaPath is null)
        {
            throw new StrategyMetaException($"Missing required argument {MetaFlag} <path>.");
        }

        if (rulesPath is null)
        {
            throw new StrategyMetaException($"Missing required argument {RulesFlag} <path>.");
        }

        return new StrategyLoaderArgs(metaPath, rulesPath);
    }

    private static string AssignOnce(string? current, string value, string flag)
    {
        if (current is not null)
        {
            throw new StrategyMetaException($"{flag} was specified more than once.");
        }

        return value;
    }
}
