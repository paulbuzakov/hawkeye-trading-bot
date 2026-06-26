using System.Diagnostics.CodeAnalysis;
using HTB.Strategy.Loader.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader;

/// <summary>
/// Console entry point for loading/validating strategies: <c>htb-strategy [--save] &lt;path&gt; [more...]</c>,
/// where each path is a <c>.htbstrat</c> package archive or a directory holding <c>meta.json</c> +
/// <c>rules.json</c>. With <c>--save</c> and <c>HTB_CONNECTION_STRING</c> set, each valid strategy is
/// persisted into the registry. Pure composition (real file I/O + console + PostgreSQL wiring), so it
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

        // The registry writer is wired only when a connection string is present; otherwise --save
        // is rejected by the runner with a usage error.
        StrategyWriteDbContext? writeDb = null;
        IStrategyStore? store = null;
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (connectionString is not null)
        {
            var options = new DbContextOptionsBuilder<StrategyWriteDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            writeDb = new StrategyWriteDbContext(options);
            store = new StrategyStore(writeDb, TimeProvider.System);
        }

        try
        {
            var runner = new StrategyLoader(packageLoader, jsonLoader, Console.Out, Console.Error, store);
            return await runner.RunAsync(args);
        }
        finally
        {
            if (writeDb is not null)
            {
                await writeDb.DisposeAsync();
            }
        }
    }
}
