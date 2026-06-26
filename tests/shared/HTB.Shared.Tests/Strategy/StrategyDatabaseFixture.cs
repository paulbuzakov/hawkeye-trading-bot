using HTB.Shared.Strategy.Persistence;
using HTB.Strategy.Loader.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace HTB.Shared.Tests.Strategy;

/// <summary>
/// Spins up a real PostgreSQL container and applies the strategy EF migrations once, creating the
/// <c>strategy</c> schema. Shared across the strategy-registry integration tests. Mirrors
/// <c>TimescaleDatabaseFixture</c>.
/// </summary>
public sealed class StrategyDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(
        "timescale/timescaledb:latest-pg17"
    ).Build();

    private string ConnectionString => _container.GetConnectionString();

    public StrategyReadonlyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StrategyReadonlyDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql =>
                    npgsql
                        .MigrationsAssembly("HTB.Strategy.Migrations")
                        .MigrationsHistoryTable("__EFMigrationsHistory", "strategy")
            )
            .Options;
        return new StrategyReadonlyDbContext(options);
    }

    public StrategyWriteDbContext CreateWriteContext()
    {
        var options = new DbContextOptionsBuilder<StrategyWriteDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new StrategyWriteDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(StrategyDatabaseCollection))]
public sealed class StrategyDatabaseCollection : ICollectionFixture<StrategyDatabaseFixture>;
