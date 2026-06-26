using HTB.MarketData.Loader.Persistence;
using HTB.Shared.MarketData.Domain;
using HTB.Shared.MarketData.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace HTB.Shared.Tests;

/// <summary>
/// Spins up a real TimescaleDB container, applies the EF migrations once, and seeds one
/// exchange + symbol so candle FKs are satisfied. Shared across the integration test class.
/// </summary>
public sealed class TimescaleDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(
        "timescale/timescaledb:latest-pg17"
    ).Build();

    public int ExchangeId { get; private set; }

    public int SymbolId { get; private set; }

    private string ConnectionString => _container.GetConnectionString();

    public MarketDataReadonlyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MarketDataReadonlyDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly("HTB.MarketData.Migrations")
            )
            .Options;
        return new MarketDataReadonlyDbContext(options);
    }

    public MarketDataWriteDbContext CreateWriteContext()
    {
        var options = new DbContextOptionsBuilder<MarketDataWriteDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new MarketDataWriteDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var exchange = new Exchange { Code = "binance", Name = "Binance" };
        context.Exchanges.Add(exchange);
        await context.SaveChangesAsync();

        var symbol = new Symbol
        {
            ExchangeId = exchange.Id,
            BaseAsset = "BTC",
            QuoteAsset = "USDT",
            ExchangeSymbol = "BTCUSDT",
        };
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        ExchangeId = exchange.Id;
        SymbolId = symbol.Id;
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(TimescaleCollection))]
public sealed class TimescaleCollection : ICollectionFixture<TimescaleDatabaseFixture>;
