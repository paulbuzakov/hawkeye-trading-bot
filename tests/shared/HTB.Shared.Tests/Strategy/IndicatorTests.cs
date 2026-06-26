using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Abstractions;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Strategy.Indicators;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class IndicatorTests
{
    private static decimal Feed(IIndicator indicator, params decimal[] closes)
    {
        foreach (var close in closes)
        {
            indicator.Add(Candle(close));
        }

        return indicator.Value;
    }

    [Fact]
    public void Indicators_reject_non_positive_period()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sma(CandleField.Close, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ema(CandleField.Close, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rsi(CandleField.Close, 0));
    }

    [Fact]
    public void Sma_averages_the_trailing_window()
    {
        var sma = new Sma(CandleField.Close, 3);

        sma.Add(Candle(1));
        Assert.False(sma.IsReady);
        Assert.Equal(1m, sma.Value);

        sma.Add(Candle(2));
        Assert.Equal(1.5m, sma.Value);

        sma.Add(Candle(3));
        Assert.True(sma.IsReady);
        Assert.Equal(2m, sma.Value);

        sma.Add(Candle(4));
        Assert.Equal(3m, sma.Value);
    }

    [Fact]
    public void Ema_seeds_with_sma_then_smooths()
    {
        var ema = new Ema(CandleField.Close, 3);

        Assert.Equal(1m, Feed(ema, 1));
        Assert.False(ema.IsReady);
        Assert.Equal(1.5m, Feed(ema, 2));
        Assert.Equal(2m, Feed(ema, 3));
        Assert.True(ema.IsReady);
        Assert.Equal(3m, Feed(ema, 4));
    }

    [Fact]
    public void Rsi_is_100_while_there_are_no_losses()
    {
        var rsi = new Rsi(CandleField.Close, 2);

        rsi.Add(Candle(1)); // primes previous, no delta yet
        Assert.False(rsi.IsReady);

        rsi.Add(Candle(2));
        Assert.Equal(100m, rsi.Value);

        rsi.Add(Candle(3));
        Assert.True(rsi.IsReady);
        Assert.Equal(100m, rsi.Value);
    }

    [Fact]
    public void Rsi_applies_wilder_smoothing_after_the_seed()
    {
        var rsi = new Rsi(CandleField.Close, 2);

        var value = Feed(rsi, 1, 2, 3, 1);

        Assert.True(rsi.IsReady);
        Assert.Equal(33.3333m, Math.Round(value, 4));
    }

    [Fact]
    public void Indicators_are_deterministic_across_two_runs()
    {
        decimal[] series = [10, 11, 9, 12, 8, 13, 7];

        Assert.Equal(Feed(new Rsi(CandleField.Close, 3), series), Feed(new Rsi(CandleField.Close, 3), series));
        Assert.Equal(Feed(new Ema(CandleField.Close, 3), series), Feed(new Ema(CandleField.Close, 3), series));
        Assert.Equal(Feed(new Sma(CandleField.Close, 3), series), Feed(new Sma(CandleField.Close, 3), series));
    }

    [Fact]
    public void Sma_reads_its_configured_source_field()
    {
        var sma = new Sma(CandleField.Volume, 1);
        sma.Add(Candle(close: 100, volume: 5));
        Assert.Equal(5m, sma.Value);
    }
}
