using HTB.MarketData.Loader.Persistence;
using HTB.Shared.MarketData.Domain;
using HTB.Shared.MarketData.Persistence;

namespace HTB.Shared.Tests;

[Collection(nameof(TimescaleCollection))]
public sealed class CandleRepositoryTests(TimescaleDatabaseFixture fixture)
{
    private static readonly DateTimeOffset Base = new(2026, 6, 24, 0, 0, 0, TimeSpan.Zero);

    private readonly TimescaleDatabaseFixture _fixture = fixture;

    private Candle NewCandle(
        Timeframe interval,
        DateTimeOffset openTime,
        decimal close,
        bool isClosed
    ) =>
        new()
        {
            ExchangeId = _fixture.ExchangeId,
            SymbolId = _fixture.SymbolId,
            Interval = interval,
            OpenTime = openTime,
            Open = 100m,
            High = 110m,
            Low = 90m,
            Close = close,
            Volume = 5m,
            QuoteVolume = 500m,
            TradeCount = 10,
            IsClosed = isClosed,
        };

    // The read repository never writes; arrange rows through the loader's write path.
    private async Task SeedAsync(params Candle[] candles)
    {
        await using var writeContext = _fixture.CreateWriteContext();
        await new CandleWriter(writeContext).UpsertAsync(candles);
    }

    [Fact]
    public async Task GetRangeAsync_filters_and_orders_by_open_time()
    {
        var t0 = Base.AddHours(2);
        await SeedAsync(
            NewCandle(Timeframe.M15, t0, 1m, isClosed: true),
            NewCandle(Timeframe.M15, t0.AddMinutes(15), 2m, isClosed: true),
            NewCandle(Timeframe.M15, t0.AddMinutes(30), 3m, isClosed: true),
            NewCandle(Timeframe.M15, t0.AddMinutes(45), 4m, isClosed: true)
        );

        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);

        var stored = await repository.GetRangeAsync(
            _fixture.SymbolId,
            Timeframe.M15,
            t0.AddMinutes(15),
            t0.AddMinutes(30)
        );

        Assert.Equal(2, stored.Count);
        Assert.Equal(t0.AddMinutes(15), stored[0].OpenTime);
        Assert.Equal(t0.AddMinutes(30), stored[1].OpenTime);
    }

    [Fact]
    public async Task GetLatestAsync_returns_most_recent_candle()
    {
        var t0 = Base.AddHours(3);
        await SeedAsync(
            NewCandle(Timeframe.H1, t0, 10m, isClosed: true),
            NewCandle(Timeframe.H1, t0.AddHours(1), 20m, isClosed: true),
            NewCandle(Timeframe.H1, t0.AddHours(2), 30m, isClosed: false)
        );

        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);

        var latest = await repository.GetLatestAsync(_fixture.SymbolId, Timeframe.H1);

        Assert.NotNull(latest);
        Assert.Equal(t0.AddHours(2), latest!.OpenTime);
        Assert.Equal(30m, latest.Close);
    }

    [Fact]
    public async Task GetLatestAsync_returns_null_when_no_candles()
    {
        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);

        var latest = await repository.GetLatestAsync(_fixture.SymbolId, Timeframe.H4);

        Assert.Null(latest);
    }

    [Fact]
    public async Task Reads_run_on_the_no_tracking_context()
    {
        var t0 = Base.AddHours(5);
        await SeedAsync(NewCandle(Timeframe.D1, t0, 42m, isClosed: true));

        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);

        var stored = await repository.GetRangeAsync(_fixture.SymbolId, Timeframe.D1, t0, t0);

        // The read context is no-tracking: queried candles never enter its change tracker.
        Assert.Single(stored);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
