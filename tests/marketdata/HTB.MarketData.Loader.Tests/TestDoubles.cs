using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using HTB.MarketData.Loader.Binance;
using HTB.MarketData.Loader.Persistence;
using HTB.Shared.MarketData.Abstractions;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Tests;

/// <summary>A clock that always reports a fixed instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>
/// Replays a queue of canned HTTP responses and records the request URIs it saw, so client
/// tests can assert on pagination query strings without touching the network.
/// </summary>
internal sealed class StubHttpMessageHandler(params HttpResponseMessage[] responses)
    : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new(responses);

    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(_responses.Dequeue());
    }

    public static HttpResponseMessage Json(
        string body,
        HttpStatusCode status = HttpStatusCode.OK
    ) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

/// <summary>Scriptable <see cref="IBinanceMarketDataClient"/> for loader tests.</summary>
internal sealed class FakeBinanceClient : IBinanceMarketDataClient
{
    public Dictionary<string, BinanceSymbolInfo> SymbolInfos { get; } = [];

    public Func<
        string,
        Timeframe,
        DateTimeOffset,
        DateTimeOffset,
        IReadOnlyList<BinanceKline>
    > Klines { get; set; } = (_, _, _, _) => [];

    /// <summary>Chunk size the scripted klines are split into pages of, mirroring Binance's limit.</summary>
    public int PageSize { get; set; } = BinanceMarketDataClient.Limit;

    public List<(
        string Ticker,
        Timeframe Interval,
        DateTimeOffset From,
        DateTimeOffset To
    )> KlineCalls { get; } = [];

    public Task<BinanceSymbolInfo> GetSymbolInfoAsync(
        string ticker,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(SymbolInfos[ticker]);

    public async IAsyncEnumerable<BinanceKlinePage> StreamKlinesAsync(
        string ticker,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        KlineCalls.Add((ticker, interval, from, to));

        var all = Klines(ticker, interval, from, to);
        if (all.Count == 0)
        {
            yield return new BinanceKlinePage([], IsFinal: true);
            yield break;
        }

        for (var offset = 0; offset < all.Count; offset += PageSize)
        {
            var page = all.Skip(offset).Take(PageSize).ToList();
            yield return new BinanceKlinePage(page, IsFinal: offset + PageSize >= all.Count);
        }
    }
}

/// <summary>Records the exchange/symbol resolution calls and hands back stable identifiers.</summary>
internal sealed class FakeInstrumentRepository : IInstrumentRepository
{
    public List<(string Code, string Name)> ExchangeCalls { get; } = [];

    public List<(int ExchangeId, string Base, string Quote, string Symbol)> SymbolCalls { get; } =
    [];

    public Task<Exchange> GetOrCreateExchangeAsync(
        string code,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        ExchangeCalls.Add((code, name));
        return Task.FromResult(
            new Exchange
            {
                Id = 1,
                Code = code,
                Name = name,
            }
        );
    }

    public Task<Symbol> GetOrCreateSymbolAsync(
        int exchangeId,
        string baseAsset,
        string quoteAsset,
        string exchangeSymbol,
        CancellationToken cancellationToken = default
    )
    {
        SymbolCalls.Add((exchangeId, baseAsset, quoteAsset, exchangeSymbol));
        return Task.FromResult(
            new Symbol
            {
                Id = 10 + SymbolCalls.Count,
                ExchangeId = exchangeId,
                BaseAsset = baseAsset,
                QuoteAsset = quoteAsset,
                ExchangeSymbol = exchangeSymbol,
            }
        );
    }
}

/// <summary>
/// Captures every upserted candle and reports the row count back. Doubles as both the read
/// gateway (<see cref="ICandleRepository"/>) and the write gateway (<see cref="ICandleWriter"/>)
/// so a single instance backs the loader's resume reads and its writes.
/// </summary>
internal sealed class FakeCandleRepository : ICandleRepository, ICandleWriter
{
    public List<Candle> Upserted { get; } = [];

    /// <summary>Size of each <see cref="UpsertAsync"/> call, so tests can assert page-by-page flushing.</summary>
    public List<int> BatchSizes { get; } = [];

    public Task<int> UpsertAsync(
        IReadOnlyCollection<Candle> candles,
        CancellationToken cancellationToken = default
    )
    {
        BatchSizes.Add(candles.Count);
        Upserted.AddRange(candles);
        return Task.FromResult(candles.Count);
    }

    public Task<IReadOnlyList<Candle>> GetRangeAsync(
        int symbolId,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task<Candle?> GetLatestAsync(
        int symbolId,
        Timeframe interval,
        CancellationToken cancellationToken = default
    )
    {
        var latest = Upserted
            .Where(c => c.SymbolId == symbolId && c.Interval == interval)
            .OrderByDescending(c => c.OpenTime)
            .FirstOrDefault();
        return Task.FromResult(latest);
    }
}
