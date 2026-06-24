namespace HTB.MarketData.Migrations;

/// <summary>
/// Design-time factory used by the EF Core CLI (<c>dotnet ef migrations</c> /
/// <c>database update</c>). The connection string is read from the
/// <c>HTB_MARKETDATA_DB</c> environment variable, falling back to a local default.
/// Migrations live in this assembly, separate from the <see cref="MarketDataDbContext"/>
/// in HTB.Shared, so the assembly name is pinned explicitly.
/// </summary>
public sealed class MarketDataDbContextFactory : IDesignTimeDbContextFactory<MarketDataDbContext>
{
    internal const string ConnectionStringEnvVar = "HTB_MARKETDATA_DB";

    internal const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=htb_marketdata;Username=postgres;Password=postgres";

    internal const string MigrationsAssembly = "HTB.MarketData.Migrations";

    public MarketDataDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvVar) ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(MigrationsAssembly))
            .Options;

        return new MarketDataDbContext(options);
    }
}
