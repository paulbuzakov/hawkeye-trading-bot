using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Binance;

/// <summary>
/// <see cref="IBinanceMarketDataClient"/> backed by Binance's public REST API. Klines are
/// fetched in pages of up to <see cref="Limit"/> bars and stitched together; the supplied
/// <see cref="HttpClient"/> must have its <see cref="HttpClient.BaseAddress"/> pointed at the
/// API host (e.g. <c>https://api.binance.com</c>).
/// </summary>
public sealed class BinanceMarketDataClient(HttpClient httpClient) : IBinanceMarketDataClient
{
    /// <summary>Binance's maximum number of klines per request.</summary>
    public const int Limit = 1000;

    private readonly HttpClient _httpClient = httpClient;

    public async Task<BinanceSymbolInfo> GetSymbolInfoAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        using var document = await GetJsonAsync(
            $"/api/v3/exchangeInfo?symbol={Uri.EscapeDataString(ticker)}",
            cancellationToken
        );

        var symbols = document.RootElement.GetProperty("symbols");
        if (symbols.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"Binance returned no symbol metadata for \"{ticker}\"."
            );
        }

        var symbol = symbols[0];
        return new BinanceSymbolInfo(
            symbol.GetProperty("symbol").GetString()!,
            symbol.GetProperty("baseAsset").GetString()!,
            symbol.GetProperty("quoteAsset").GetString()!
        );
    }

    public async IAsyncEnumerable<BinanceKlinePage> StreamKlinesAsync(
        string ticker,
        Timeframe interval,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        var code = BinanceIntervals.ToCode(interval);
        var endMs = to.ToUnixTimeMilliseconds();
        var startMs = from.ToUnixTimeMilliseconds();

        while (startMs <= endMs)
        {
            var url =
                $"/api/v3/klines?symbol={Uri.EscapeDataString(ticker)}&interval={code}"
                + $"&startTime={startMs}&endTime={endMs}&limit={Limit}";

            int count;
            var page = new List<BinanceKline>(Limit);
            using (var document = await GetJsonAsync(url, cancellationToken))
            {
                var batch = document.RootElement;
                count = batch.GetArrayLength();
                foreach (var element in batch.EnumerateArray())
                {
                    page.Add(ParseKline(element));
                }
            }

            // An empty page means there is no data left in the range; emit it as the final page
            // so callers still see a completion signal.
            if (count == 0)
            {
                yield return new BinanceKlinePage(page, IsFinal: true);
                yield break;
            }

            // A short page, or one whose last bar reaches the end of the range, is the last page.
            var nextStart = page[^1].OpenTime.ToUnixTimeMilliseconds() + 1;
            var isFinal = count < Limit || nextStart > endMs;
            yield return new BinanceKlinePage(page, isFinal);
            if (isFinal)
            {
                yield break;
            }

            // Advance past the last bar we just read to fetch the next page.
            startMs = nextStart;
        }

        // Reached only when from > to: no request was made, but report a completed empty range.
        yield return new BinanceKlinePage([], IsFinal: true);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
    }

    private static BinanceKline ParseKline(JsonElement element) =>
        new(
            DateTimeOffset.FromUnixTimeMilliseconds(element[0].GetInt64()),
            ParseDecimal(element[1]),
            ParseDecimal(element[2]),
            ParseDecimal(element[3]),
            ParseDecimal(element[4]),
            ParseDecimal(element[5]),
            ParseDecimal(element[7]),
            element[8].GetInt32(),
            DateTimeOffset.FromUnixTimeMilliseconds(element[6].GetInt64())
        );

    private static decimal ParseDecimal(JsonElement element) =>
        decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture);
}
