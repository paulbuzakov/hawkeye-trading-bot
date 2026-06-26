using HTB.MarketData.Loader.Persistence;
using HTB.Shared.MarketData.Domain;
using HTB.Shared.MarketData.Persistence;

namespace HTB.Shared.Tests;

[Collection(nameof(TimescaleCollection))]
public sealed class CandleWriterTests(TimescaleDatabaseFixture fixture)
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

    private async Task<IReadOnlyList<Candle>> ReadRangeAsync(
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to
    )
    {
        await using var context = _fixture.CreateContext();
        return await new CandleRepository(context).GetRangeAsync(
            _fixture.SymbolId,
            interval,
            from,
            to
        );
    }

    [Fact]
    public async Task UpsertAsync_inserts_new_candles()
    {
        await using var context = _fixture.CreateWriteContext();
        var writer = new CandleWriter(context);

        var affected = await writer.UpsertAsync([
            NewCandle(Timeframe.M1, Base, 101m, isClosed: true),
            NewCandle(Timeframe.M1, Base.AddMinutes(1), 102m, isClosed: true),
        ]);

        Assert.Equal(2, affected);

        var stored = await ReadRangeAsync(Timeframe.M1, Base, Base.AddMinutes(1));
        Assert.Equal(2, stored.Count);
        Assert.Equal(101m, stored[0].Close);
        Assert.Equal(102m, stored[1].Close);
    }

    [Fact]
    public async Task UpsertAsync_is_idempotent_and_overwrites_open_candle()
    {
        await using var context = _fixture.CreateWriteContext();
        var writer = new CandleWriter(context);
        var openTime = Base.AddHours(1);

        await writer.UpsertAsync([NewCandle(Timeframe.M5, openTime, close: 200m, isClosed: false)]);

        // Same natural key: the still-forming candle is overwritten, not duplicated.
        await writer.UpsertAsync([NewCandle(Timeframe.M5, openTime, close: 250m, isClosed: true)]);

        var stored = await ReadRangeAsync(Timeframe.M5, openTime, openTime);

        var only = Assert.Single(stored);
        Assert.Equal(250m, only.Close);
        Assert.True(only.IsClosed);
    }

    [Fact]
    public async Task UpsertAsync_with_no_candles_returns_zero()
    {
        await using var context = _fixture.CreateWriteContext();
        var writer = new CandleWriter(context);

        var affected = await writer.UpsertAsync([]);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task UpsertAsync_with_null_throws()
    {
        await using var context = _fixture.CreateWriteContext();
        var writer = new CandleWriter(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => writer.UpsertAsync(null!));
    }
}
