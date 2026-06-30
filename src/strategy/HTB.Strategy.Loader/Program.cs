using System.Diagnostics.CodeAnalysis;
using HTB.Strategy.Loader.Configuration;
using HTB.Strategy.Loader.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader;

/// <summary>
/// Console entry point for the strategy loader. Reads a strategy bundle's <c>meta.json</c> via
/// <c>--meta &lt;path&gt;</c> and its <c>rules.json</c> via <c>--rules &lt;path&gt;</c>, parses
/// them into a <c>StrategyDefinition</c> and a <c>StrategyRuleSet</c>, and persists both to the
/// strategy store in one transaction. Pure composition (arg wiring + PostgreSQL wiring + console
/// output), so it is excluded from coverage; the testable logic lives in the parsers and the repository.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class Program
{
    private const string ConnectionStringEnvVar = "HTB_CONNECTION_STRING";
    internal const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=hawkeye;Username=hawkeye;Password=hawkeye;";

    private static async Task<int> Main(string[] args)
    {
        try
        {
            var parsed = StrategyLoaderArgs.Parse(args);
            var definition = await StrategyMetaParser.ParseFileAsync(parsed.MetaPath);
            var ruleSet = await StrategyRulesParser.ParseFileAsync(parsed.RulesPath, definition.VersionId);

            var connectionString =
                Environment.GetEnvironmentVariable(ConnectionStringEnvVar) ?? DefaultConnectionString;

            var options = new DbContextOptionsBuilder<StrategyWriteDbContext>().UseNpgsql(connectionString).Options;

            await using var db = new StrategyWriteDbContext(options);
            var repository = new StrategyDefinitionRepository(db);
            var outcome = await repository.SaveAsync(definition, ruleSet);

            Console.WriteLine(
                $"{outcome} strategy {definition.VersionId} — {definition.Name} "
                    + $"({parsed.MetaPath} + {parsed.RulesPath})."
            );
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Load failed: {ex.Message}");
            return 1;
        }
    }
}
