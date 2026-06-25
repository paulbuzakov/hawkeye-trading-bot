using HTB.Shared.MarketData.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace HTB.Shared.Tests;

/// <summary>
/// Exercises the InitialCreate migration's <c>Down</c> path against a real TimescaleDB: applying
/// then reverting the migration must drop every table it created. Uses its own throwaway container
/// so reverting doesn't disturb the shared <see cref="TimescaleDatabaseFixture"/>.
/// </summary>
public sealed class MigrationDownTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:latest-pg17")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Down_drops_every_table_the_migration_created()
    {
        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsAssembly("HTB.MarketData.Migrations")
            )
            .Options;
        await using var context = new MarketDataDbContext(options);

        await context.Database.MigrateAsync();
        Assert.Equal(3, await CountMarketdataTablesAsync(context));

        // Revert to the empty schema, which runs InitialCreate.Down.
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);

        Assert.Equal(0, await CountMarketdataTablesAsync(context));
    }

    private static async Task<int> CountMarketdataTablesAsync(MarketDataDbContext context) =>
        await context
            .Database.SqlQuery<int>(
                $"""
                SELECT count(*)::int AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'marketdata'
                """
            )
            .SingleAsync();
}
