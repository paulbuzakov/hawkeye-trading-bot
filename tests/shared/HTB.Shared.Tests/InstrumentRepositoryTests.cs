using HTB.MarketData.Loader.Persistence;

namespace HTB.Shared.Tests;

[Collection(nameof(TimescaleCollection))]
public sealed class InstrumentRepositoryTests(TimescaleDatabaseFixture fixture)
{
    private readonly TimescaleDatabaseFixture _fixture = fixture;

    [Fact]
    public async Task GetOrCreateExchangeAsync_returns_the_seeded_exchange()
    {
        await using var context = _fixture.CreateWriteContext();
        var repository = new InstrumentRepository(context);

        var exchange = await repository.GetOrCreateExchangeAsync("binance", "Binance");

        Assert.Equal(_fixture.ExchangeId, exchange.Id);
    }

    [Fact]
    public async Task GetOrCreateExchangeAsync_inserts_a_new_exchange_once()
    {
        await using var context = _fixture.CreateWriteContext();
        var repository = new InstrumentRepository(context);

        var first = await repository.GetOrCreateExchangeAsync("kraken", "Kraken");
        var second = await repository.GetOrCreateExchangeAsync("kraken", "Kraken");

        Assert.True(first.Id > 0);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Kraken", first.Name);
    }

    [Fact]
    public async Task GetOrCreateExchangeAsync_blank_code_throws()
    {
        await using var context = _fixture.CreateWriteContext();
        var repository = new InstrumentRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetOrCreateExchangeAsync(" ", "Whatever")
        );
    }

    [Fact]
    public async Task GetOrCreateSymbolAsync_returns_the_seeded_symbol()
    {
        await using var context = _fixture.CreateWriteContext();
        var repository = new InstrumentRepository(context);

        var symbol = await repository.GetOrCreateSymbolAsync(
            _fixture.ExchangeId,
            "BTC",
            "USDT",
            "BTCUSDT"
        );

        Assert.Equal(_fixture.SymbolId, symbol.Id);
    }

    [Fact]
    public async Task GetOrCreateSymbolAsync_inserts_a_new_symbol_once()
    {
        await using var context = _fixture.CreateWriteContext();
        var repository = new InstrumentRepository(context);

        var first = await repository.GetOrCreateSymbolAsync(
            _fixture.ExchangeId,
            "ETH",
            "USDT",
            "ETHUSDT"
        );
        var second = await repository.GetOrCreateSymbolAsync(
            _fixture.ExchangeId,
            "ETH",
            "USDT",
            "ETHUSDT"
        );

        Assert.True(first.Id > 0);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("ETH", first.BaseAsset);
        Assert.Equal("USDT", first.QuoteAsset);
    }

    [Fact]
    public async Task GetOrCreateSymbolAsync_blank_symbol_throws()
    {
        await using var context = _fixture.CreateWriteContext();
        var repository = new InstrumentRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetOrCreateSymbolAsync(_fixture.ExchangeId, "BTC", "USDT", "")
        );
    }
}
