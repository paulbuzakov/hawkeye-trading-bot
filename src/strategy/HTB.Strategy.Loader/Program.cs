using System.Diagnostics.CodeAnalysis;
using HTB.Strategy.Loader.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader;

/// <summary>
/// Console entry point for loading/validating strategies: <c>htb-strategy [--save] &lt;path&gt; [more...]</c>,
/// where each path is a <c>.htbstrat</c> package archive or a directory holding <c>meta.json</c> +
/// <c>rules.json</c>. With <c>--save</c>, each valid strategy is persisted into the registry, using
/// <c>HTB_CONNECTION_STRING</c> when set or a local default otherwise. Pure composition (real file I/O
/// + console + PostgreSQL wiring), so it
/// is excluded from coverage; the testable logic lives in <see cref="StrategyLoader"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class Program
{
    private const string ConnectionStringEnvVar = "HTB_CONNECTION_STRING";

    private static async Task<int> Main(string[] args)
    {
        // One stateless JSON loader feeds both the package path and the loose-pair path.
        var jsonLoader = new Shared.Strategy.Strategy.StrategyLoader();
        var packageLoader = new Shared.Strategy.Strategy.StrategyPackageLoader(jsonLoader);

        // The registry writer is always wired: HTB_CONNECTION_STRING when set, or a local default
        // otherwise, so --save works out of the box against a local PostgreSQL instance.
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? "Database=hawkeye;Host=localhost;Port=5432;Username=hawkeye;Password=hawkeye";
        var options = new DbContextOptionsBuilder<StrategyWriteDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var writeDb = new StrategyWriteDbContext(options);
        IStrategyStore store = new StrategyStore(writeDb, TimeProvider.System);

        var runner = new StrategyLoader(
            packageLoader,
            jsonLoader,
            Console.Out,
            Console.Error,
            store
        );
        return await runner.RunAsync(args);
    }
}
