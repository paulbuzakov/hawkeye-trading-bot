using HTB.Strategy.Shared.Persistence;

namespace HTB.Strategy.Migrations;

/// <summary>
/// Design-time factory used by the EF Core CLI (<c>dotnet ef migrations</c> /
/// <c>database update</c>). The connection string is read from the
/// <c>HTB_CONNECTION_STRING</c> environment variable, falling back to a local default.
/// Migrations target the read-only <see cref="StrategyReadonlyDbContext"/> (the migration
/// target that owns the snapshot) and live in this assembly, separate from the context in
/// HTB.Strategy.Shared, so the assembly name is pinned explicitly.
/// </summary>
public sealed class StrategyReadonlyDbContextFactory : IDesignTimeDbContextFactory<StrategyReadonlyDbContext>
{
    internal const string ConnectionStringEnvVar = "HTB_CONNECTION_STRING";
    internal const string MigrationsAssembly = "HTB.Strategy.Migrations";

    // Keep the EF history table inside the schema it tracks rather than the default
    // "public", so each store's migration bookkeeping lives next to its own tables.
    internal const string HistoryTableName = "__EFMigrationsHistory";
    internal const string HistoryTableSchema = "strategy";

    // Used at build time (e.g. `dotnet ef migrations bundle`) when no env var is
    // present. EF only needs a syntactically valid string to construct the model;
    // it never opens a connection. The real connection is supplied at runtime via
    // HTB_CONNECTION_STRING or the bundle's `--connection` flag.
    internal const string DefaultConnectionString = "Host=localhost;Port=5432;Database=hawkeye;Username=hawkeye";

    public StrategyReadonlyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar) ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<StrategyReadonlyDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql =>
                    npgsql
                        .MigrationsAssembly(MigrationsAssembly)
                        .MigrationsHistoryTable(HistoryTableName, HistoryTableSchema)
            )
            .Options;

        return new StrategyReadonlyDbContext(options);
    }
}
