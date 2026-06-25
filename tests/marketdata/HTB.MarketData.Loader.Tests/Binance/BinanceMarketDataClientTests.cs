using System.Globalization;
using System.Net;
using System.Text;
using HTB.MarketData.Loader.Binance;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Tests.Binance;

public class BinanceMarketDataClientTests
{
    private static HttpClient ClientFor(StubHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.binance.com") };

    private static string Kline(long openMs, decimal close, int trades, long closeMs) =>
        $"[{openMs},\"100.0\",\"110.0\",\"90.0\",\"{close.ToString(CultureInfo.InvariantCulture)}\","
        + $"\"5.0\",{closeMs},\"500.0\",{trades}]";

    private static string KlinePage(long firstOpenMs, long stepMs, int count)
    {
        var items = Enumerable
            .Range(0, count)
            .Select(i =>
            {
                var open = firstOpenMs + (i * stepMs);
                return Kline(open, 100m + i, 1, open + stepMs - 1);
            });
        return "[" + string.Join(",", items) + "]";
    }

    private static async Task<List<BinanceKlinePage>> CollectAsync(
        IAsyncEnumerable<BinanceKlinePage> stream
    )
    {
        var pages = new List<BinanceKlinePage>();
        await foreach (var page in stream)
        {
            pages.Add(page);
        }

        return pages;
    }

    private static List<BinanceKline> Flatten(IEnumerable<BinanceKlinePage> pages) =>
        pages.SelectMany(p => p.Klines).ToList();

    [Fact]
    public async Task GetSymbolInfoAsync_returns_asset_breakdown()
    {
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(
                """{ "symbols": [ { "symbol": "BTCUSDT", "baseAsset": "BTC", "quoteAsset": "USDT" } ] }"""
            )
        );
        var client = new BinanceMarketDataClient(ClientFor(handler));

        var info = await client.GetSymbolInfoAsync("BTCUSDT");

        Assert.Equal(new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT"), info);
        Assert.Contains("symbol=BTCUSDT", handler.Requests[0].Query);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_no_symbols_throws()
    {
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("""{ "symbols": [] }""")
        );
        var client = new BinanceMarketDataClient(ClientFor(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetSymbolInfoAsync("NOPE")
        );
    }

    [Fact]
    public async Task GetSymbolInfoAsync_http_error_throws()
    {
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("bad request", HttpStatusCode.BadRequest)
        );
        var client = new BinanceMarketDataClient(ClientFor(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetSymbolInfoAsync("BTCUSDT"));
    }

    [Fact]
    public async Task GetSymbolInfoAsync_blank_ticker_throws()
    {
        var client = new BinanceMarketDataClient(ClientFor(new StubHttpMessageHandler()));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetSymbolInfoAsync(" "));
    }

    [Fact]
    public async Task StreamKlinesAsync_parses_a_single_page()
    {
        const long openMs = 1_577_836_800_000; // 2020-01-01T00:00:00Z
        const long closeMs = openMs + 59_999;
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json($"[{Kline(openMs, 105.5m, 42, closeMs)}]")
        );
        var client = new BinanceMarketDataClient(ClientFor(handler));

        var pages = await CollectAsync(
            client.StreamKlinesAsync(
                "BTCUSDT",
                Timeframe.M1,
                DateTimeOffset.FromUnixTimeMilliseconds(openMs),
                DateTimeOffset.FromUnixTimeMilliseconds(closeMs)
            )
        );

        Assert.True(Assert.Single(pages).IsFinal);
        var kline = Assert.Single(Flatten(pages));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(openMs), kline.OpenTime);
        Assert.Equal(100.0m, kline.Open);
        Assert.Equal(110.0m, kline.High);
        Assert.Equal(90.0m, kline.Low);
        Assert.Equal(105.5m, kline.Close);
        Assert.Equal(5.0m, kline.Volume);
        Assert.Equal(500.0m, kline.QuoteVolume);
        Assert.Equal(42, kline.TradeCount);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(closeMs), kline.CloseTime);

        Assert.Single(handler.Requests);
        Assert.Contains("interval=1m", handler.Requests[0].Query);
    }

    [Fact]
    public async Task StreamKlinesAsync_paginates_across_pages()
    {
        const long firstOpen = 1_577_836_800_000;
        const long step = 60_000;
        // A full page forces a follow-up request; the short second page ends pagination.
        var page1 = KlinePage(firstOpen, step, BinanceMarketDataClient.Limit);
        var page2Open = firstOpen + (BinanceMarketDataClient.Limit * step);
        var page2 = KlinePage(page2Open, step, 3);

        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(page1),
            StubHttpMessageHandler.Json(page2)
        );
        var client = new BinanceMarketDataClient(ClientFor(handler));

        var pages = await CollectAsync(
            client.StreamKlinesAsync(
                "BTCUSDT",
                Timeframe.M1,
                DateTimeOffset.FromUnixTimeMilliseconds(firstOpen),
                DateTimeOffset.FromUnixTimeMilliseconds(page2Open + (3 * step))
            )
        );

        // One page per Binance request, only the last flagged final.
        Assert.Equal([false, true], pages.Select(p => p.IsFinal));
        Assert.Equal(BinanceMarketDataClient.Limit + 3, Flatten(pages).Count);
        Assert.Equal(2, handler.Requests.Count);

        // The second request starts one ms after the last bar of page one.
        var lastOpenOfPage1 = firstOpen + ((BinanceMarketDataClient.Limit - 1) * step);
        Assert.Contains($"startTime={lastOpenOfPage1 + 1}", handler.Requests[1].Query);
    }

    [Fact]
    public async Task StreamKlinesAsync_emits_a_final_empty_page_on_empty_response()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json("[]"));
        var client = new BinanceMarketDataClient(ClientFor(handler));

        var pages = await CollectAsync(
            client.StreamKlinesAsync(
                "BTCUSDT",
                Timeframe.H1,
                DateTimeOffset.FromUnixTimeMilliseconds(0),
                DateTimeOffset.FromUnixTimeMilliseconds(1_000_000)
            )
        );

        Assert.True(Assert.Single(pages).IsFinal);
        Assert.Empty(Flatten(pages));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task StreamKlinesAsync_emits_a_final_empty_page_when_from_after_to()
    {
        var handler = new StubHttpMessageHandler();
        var client = new BinanceMarketDataClient(ClientFor(handler));

        var pages = await CollectAsync(
            client.StreamKlinesAsync(
                "BTCUSDT",
                Timeframe.M1,
                DateTimeOffset.FromUnixTimeMilliseconds(2_000),
                DateTimeOffset.FromUnixTimeMilliseconds(1_000)
            )
        );

        Assert.True(Assert.Single(pages).IsFinal);
        Assert.Empty(Flatten(pages));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task StreamKlinesAsync_blank_ticker_throws()
    {
        var client = new BinanceMarketDataClient(ClientFor(new StubHttpMessageHandler()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => CollectAsync(client.StreamKlinesAsync("", Timeframe.M1, default, default))
        );
    }
}
