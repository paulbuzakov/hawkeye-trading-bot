using HTB.MarketData.Loader.Binance;
using HTB.MarketData.Loader.Configuration;
using HTB.MarketData.Loader.Ingestion;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Tests.Ingestion;

public class MarketDataLoaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);

    private static BinanceKline KlineAt(DateTimeOffset open, DateTimeOffset close, decimal price) =>
        new(open, price, price + 10m, price - 10m, price, 5m, 500m, 7, close);

    [Fact]
    public async Task LoadAsync_resolves_instruments_and_upserts_each_timeframe()
    {
        var client = new FakeBinanceClient();
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        client.Klines = (_, interval, _, _) =>
            [
                KlineAt(
                    Now.AddDays(-2),
                    Now.AddDays(-2).AddMinutes(1),
                    interval == Timeframe.M1 ? 100m : 200m
                ),
            ];

        var instruments = new FakeInstrumentRepository();
        var candles = new FakeCandleRepository();
        var log = new List<string>();
        var loader = new MarketDataLoader(
            client,
            instruments,
            candles,
            new FixedTimeProvider(Now),
            log.Add
        );

        var specs = new[]
        {
            new SymbolLoadSpec(
                "BTCUSDT",
                [Timeframe.M1, Timeframe.M5],
                Now.AddDays(-10),
                Now.AddDays(-1)
            ),
        };

        var written = await loader.LoadAsync(specs);

        Assert.Equal(2, written);
        Assert.Equal(("binance", "Binance"), Assert.Single(instruments.ExchangeCalls));
        Assert.Equal((1, "BTC", "USDT", "BTCUSDT"), Assert.Single(instruments.SymbolCalls));

        Assert.Equal(2, candles.Upserted.Count);
        Assert.All(candles.Upserted, c => Assert.Equal(1, c.ExchangeId));
        Assert.All(candles.Upserted, c => Assert.Equal(11, c.SymbolId));
        Assert.Contains(candles.Upserted, c => c.Interval == Timeframe.M1 && c.Close == 100m);
        Assert.Contains(candles.Upserted, c => c.Interval == Timeframe.M5 && c.Close == 200m);

        Assert.Equal(2, log.Count);
        Assert.StartsWith("[100%] BTCUSDT M1: 1 candles", log[0]);
        Assert.StartsWith("[100%] BTCUSDT M5: 1 candles", log[1]);
    }

    [Fact]
    public async Task LoadAsync_flushes_each_page_and_reports_progress_per_page()
    {
        var from = Now.AddDays(-1);
        var to = Now; // A 24h window, so a bar opening at +Nh sits at N/24 of the way through.

        var client = new FakeBinanceClient { PageSize = 1 };
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        client.Klines = (_, _, _, _) =>
            [
                KlineAt(from.AddHours(6), from.AddHours(6).AddMinutes(1), 100m),
                KlineAt(from.AddHours(12), from.AddHours(12).AddMinutes(1), 101m),
                KlineAt(from.AddHours(18), from.AddHours(18).AddMinutes(1), 102m),
                KlineAt(from.AddHours(23), from.AddHours(23).AddMinutes(1), 103m),
            ];

        var candles = new FakeCandleRepository();
        var log = new List<string>();
        var loader = new MarketDataLoader(
            client,
            new FakeInstrumentRepository(),
            candles,
            new FixedTimeProvider(Now),
            log.Add
        );

        var written = await loader.LoadAsync([
            new SymbolLoadSpec("BTCUSDT", [Timeframe.M1], from, to),
        ]);

        Assert.Equal(4, written);

        // Each page is persisted on its own, not buffered into one write.
        Assert.Equal([1, 1, 1, 1], candles.BatchSizes);

        // Intermediate pages report time-based progress; the final page snaps to 100%.
        Assert.Equal(
            [
                "[ 25%] BTCUSDT M1: 1 candles",
                "[ 50%] BTCUSDT M1: 2 candles",
                "[ 75%] BTCUSDT M1: 3 candles",
                "[100%] BTCUSDT M1: 4 candles",
            ],
            log
        );
    }

    [Fact]
    public async Task LoadAsync_reports_full_progress_for_a_zero_length_window()
    {
        // A degenerate from == to range can't be expressed as a fraction; every page reads 100%.
        var client = new FakeBinanceClient { PageSize = 1 };
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        client.Klines = (_, _, _, _) =>
            [KlineAt(Now, Now.AddMinutes(1), 100m), KlineAt(Now, Now.AddMinutes(2), 101m)];

        var log = new List<string>();
        var loader = new MarketDataLoader(
            client,
            new FakeInstrumentRepository(),
            new FakeCandleRepository(),
            new FixedTimeProvider(Now),
            log.Add
        );

        await loader.LoadAsync([new SymbolLoadSpec("BTCUSDT", [Timeframe.M1], Now, Now)]);

        Assert.All(log, line => Assert.StartsWith("[100%]", line));
    }

    [Fact]
    public async Task LoadAsync_resumes_from_the_last_stored_candle_on_restart()
    {
        var client = new FakeBinanceClient();
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        client.Klines = (_, _, _, _) => [KlineAt(Now.AddHours(-1), Now.AddMinutes(-30), 200m)];

        // Simulate a prior run: a bar that opened two hours ago is already stored.
        var lastStored = Now.AddHours(-2);
        var candles = new FakeCandleRepository();
        candles.Upserted.Add(
            new Candle
            {
                SymbolId = 11,
                Interval = Timeframe.H1,
                OpenTime = lastStored,
            }
        );

        var loader = new MarketDataLoader(
            client,
            new FakeInstrumentRepository(),
            candles,
            new FixedTimeProvider(Now)
        );

        await loader.LoadAsync([
            new SymbolLoadSpec("BTCUSDT", [Timeframe.H1], Now.AddDays(-10), Now),
        ]);

        // The fetch resumes from the last stored bar, not the manifest's far-back From.
        Assert.Equal(lastStored, Assert.Single(client.KlineCalls).From);
    }

    [Fact]
    public async Task LoadAsync_backfills_from_spec_start_when_no_prior_data()
    {
        var client = new FakeBinanceClient();
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        var from = Now.AddDays(-10);

        var loader = new MarketDataLoader(
            client,
            new FakeInstrumentRepository(),
            new FakeCandleRepository(),
            new FixedTimeProvider(Now)
        );

        await loader.LoadAsync([new SymbolLoadSpec("BTCUSDT", [Timeframe.H1], from, Now)]);

        Assert.Equal(from, Assert.Single(client.KlineCalls).From);
    }

    [Fact]
    public async Task LoadAsync_persists_closed_bars_and_skips_the_forming_bar()
    {
        var client = new FakeBinanceClient();
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        client.Klines = (_, _, _, _) =>
            [
                KlineAt(Now.AddHours(-2), Now.AddHours(-1), 100m), // closed: close time in the past
                KlineAt(Now.AddMinutes(-1), Now.AddMinutes(1), 105m), // forming: close time in the future
            ];

        var candles = new FakeCandleRepository();
        var loader = new MarketDataLoader(
            client,
            new FakeInstrumentRepository(),
            candles,
            new FixedTimeProvider(Now)
        );

        var written = await loader.LoadAsync([
            new SymbolLoadSpec("BTCUSDT", [Timeframe.H1], Now.AddDays(-1), Now),
        ]);

        // Only the closed bar is stored; the still-forming one is dropped.
        Assert.Equal(1, written);
        var stored = Assert.Single(candles.Upserted);
        Assert.Equal(100m, stored.Close);
        Assert.True(stored.IsClosed);
    }

    [Fact]
    public async Task LoadAsync_defaults_upper_bound_to_now_when_to_is_null()
    {
        var client = new FakeBinanceClient();
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");

        var loader = new MarketDataLoader(
            client,
            new FakeInstrumentRepository(),
            new FakeCandleRepository(),
            new FixedTimeProvider(Now)
        );

        await loader.LoadAsync([
            new SymbolLoadSpec("BTCUSDT", [Timeframe.M1], Now.AddDays(-3), To: null),
        ]);

        Assert.Equal(Now, Assert.Single(client.KlineCalls).To);
    }

    [Fact]
    public async Task LoadAsync_uses_explicit_to_when_provided()
    {
        var client = new FakeBinanceClient();
        client.SymbolInfos["BTCUSDT"] = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        var to = Now.AddDays(-1);

        var loader = new MarketDataLoader(
            client,
            new FakeInstrumentRepository(),
            new FakeCandleRepository(),
            new FixedTimeProvider(Now)
        );

        await loader.LoadAsync([
            new SymbolLoadSpec("BTCUSDT", [Timeframe.M1], Now.AddDays(-3), to),
        ]);

        Assert.Equal(to, Assert.Single(client.KlineCalls).To);
    }

    [Fact]
    public async Task LoadAsync_empty_specs_writes_nothing()
    {
        var loader = new MarketDataLoader(
            new FakeBinanceClient(),
            new FakeInstrumentRepository(),
            new FakeCandleRepository(),
            new FixedTimeProvider(Now)
        );

        var written = await loader.LoadAsync([]);

        Assert.Equal(0, written);
    }

    [Fact]
    public async Task LoadAsync_null_specs_throws()
    {
        var loader = new MarketDataLoader(
            new FakeBinanceClient(),
            new FakeInstrumentRepository(),
            new FakeCandleRepository(),
            new FixedTimeProvider(Now)
        );

        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadAsync(null!));
    }
}
