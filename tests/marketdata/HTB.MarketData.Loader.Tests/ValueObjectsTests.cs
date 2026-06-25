using HTB.MarketData.Loader.Binance;
using HTB.MarketData.Loader.Configuration;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Tests;

/// <summary>
/// Exercises the value semantics (equality, hashing, <c>with</c>, deconstruction, ToString) of
/// the loader's record value objects.
/// </summary>
public class ValueObjectsTests
{
    [Fact]
    public void SymbolLoadSpec_has_value_semantics()
    {
        var from = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new SymbolLoadSpec("BTCUSDT", [Timeframe.M1], from, null);
        var b = a with { To = from.AddDays(1) };

        Assert.Equal(a, new SymbolLoadSpec("BTCUSDT", a.Timeframes, from, null));
        Assert.NotEqual(a, b);
        Assert.Equal(a.GetHashCode(), a.GetHashCode());
        Assert.Equal("BTCUSDT", a.Ticker);
        Assert.NotNull(b.To);
    }

    [Fact]
    public void BinanceSymbolInfo_has_value_semantics()
    {
        var a = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");
        var b = new BinanceSymbolInfo("BTCUSDT", "BTC", "USDT");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Contains("BTCUSDT", a.ToString());

        var (symbol, baseAsset, quoteAsset) = a;
        Assert.Equal("BTCUSDT", symbol);
        Assert.Equal("BTC", baseAsset);
        Assert.Equal("USDT", quoteAsset);
    }

    [Fact]
    public void BinanceKline_has_value_semantics()
    {
        var open = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var close = open.AddMinutes(1);
        var a = new BinanceKline(open, 1m, 2m, 0.5m, 1.5m, 5m, 500m, 7, close);
        var b = a with { Close = 9m };

        Assert.NotEqual(a, b);
        Assert.Equal(a, a with { });
        Assert.Equal(9m, b.Close);
        Assert.Equal(7, a.TradeCount);
        Assert.Contains("TradeCount", a.ToString());
    }
}
