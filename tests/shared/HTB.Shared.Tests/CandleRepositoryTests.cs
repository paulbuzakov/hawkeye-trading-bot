using HTB.Shared.MarketData;

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

    [Fact]
    public async Task UpsertAsync_inserts_new_candles()
    {
        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);

        var candles = new[]
        {
            NewCandle(Timeframe.M1, Base, 101m, isClosed: true),
            NewCandle(Timeframe.M1, Base.AddMinutes(1), 102m, isClosed: true),
        };

        var affected = await repository.UpsertAsync(candles);

        Assert.Equal(2, affected);

        var stored = await repository.GetRangeAsync(
            _fixture.SymbolId,
            Timeframe.M1,
            Base,
            Base.AddMinutes(1)
        );
        Assert.Equal(2, stored.Count);
        Assert.Equal(101m, stored[0].Close);
        Assert.Equal(102m, stored[1].Close);
    }

    [Fact]
    public async Task UpsertAsync_is_idempotent_and_overwrites_open_candle()
    {
        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);
        var openTime = Base.AddHours(1);

        await repository.UpsertAsync([
            NewCandle(Timeframe.M5, openTime, close: 200m, isClosed: false),
        ]);

        // Same natural key: the still-forming candle is overwritten, not duplicated.
        await repository.UpsertAsync([
            NewCandle(Timeframe.M5, openTime, close: 250m, isClosed: true),
        ]);

        var stored = await repository.GetRangeAsync(
            _fixture.SymbolId,
            Timeframe.M5,
            openTime,
            openTime
        );

        var only = Assert.Single(stored);
        Assert.Equal(250m, only.Close);
        Assert.True(only.IsClosed);
    }

    [Fact]
    public async Task UpsertAsync_with_no_candles_returns_zero()
    {
        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);

        var affected = await repository.UpsertAsync([]);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task UpsertAsync_with_null_throws()
    {
        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpsertAsync(null!));
    }

    [Fact]
    public async Task GetRangeAsync_filters_and_orders_by_open_time()
    {
        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);
        var t0 = Base.AddHours(2);

        await repository.UpsertAsync([
            NewCandle(Timeframe.M15, t0, 1m, isClosed: true),
            NewCandle(Timeframe.M15, t0.AddMinutes(15), 2m, isClosed: true),
            NewCandle(Timeframe.M15, t0.AddMinutes(30), 3m, isClosed: true),
            NewCandle(Timeframe.M15, t0.AddMinutes(45), 4m, isClosed: true),
        ]);

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
        await using var context = _fixture.CreateContext();
        var repository = new CandleRepository(context);
        var t0 = Base.AddHours(3);

        await repository.UpsertAsync([
            NewCandle(Timeframe.H1, t0, 10m, isClosed: true),
            NewCandle(Timeframe.H1, t0.AddHours(1), 20m, isClosed: true),
            NewCandle(Timeframe.H1, t0.AddHours(2), 30m, isClosed: false),
        ]);

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
}
