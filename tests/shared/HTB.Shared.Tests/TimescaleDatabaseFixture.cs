using HTB.Shared.MarketData;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace HTB.Shared.Tests;

/// <summary>
/// Spins up a real TimescaleDB container, applies the EF migrations once, and seeds one
/// exchange + symbol so candle FKs are satisfied. Shared across the integration test class.
/// </summary>
public sealed class TimescaleDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:latest-pg17")
        .Build();

    public int ExchangeId { get; private set; }

    public int SymbolId { get; private set; }

    private string ConnectionString => _container.GetConnectionString();

    public MarketDataDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly("HTB.MarketData.Migrations")
            )
            .Options;
        return new MarketDataDbContext(options);
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
