using HTB.Shared.MarketData.Domain;

namespace HTB.Shared.Tests;

public class MarketDataModelTests
{
    [Fact]
    public void Exchange_round_trips_all_properties()
    {
        var exchange = new Exchange
        {
            Id = 7,
            Code = "binance",
            Name = "Binance",
        };

        Assert.Equal(7, exchange.Id);
        Assert.Equal("binance", exchange.Code);
        Assert.Equal("Binance", exchange.Name);
    }

    [Fact]
    public void Exchange_string_properties_default_to_empty()
    {
        var exchange = new Exchange();

        Assert.Equal(string.Empty, exchange.Code);
        Assert.Equal(string.Empty, exchange.Name);
    }

    [Fact]
    public void Symbol_round_trips_all_properties()
    {
        var symbol = new Symbol
        {
            Id = 42,
            ExchangeId = 7,
            BaseAsset = "BTC",
            QuoteAsset = "USDT",
            ExchangeSymbol = "BTCUSDT",
        };

        Assert.Equal(42, symbol.Id);
        Assert.Equal(7, symbol.ExchangeId);
        Assert.Equal("BTC", symbol.BaseAsset);
        Assert.Equal("USDT", symbol.QuoteAsset);
        Assert.Equal("BTCUSDT", symbol.ExchangeSymbol);
    }

    [Fact]
    public void Symbol_string_properties_default_to_empty()
    {
        var symbol = new Symbol();

        Assert.Equal(string.Empty, symbol.BaseAsset);
        Assert.Equal(string.Empty, symbol.QuoteAsset);
        Assert.Equal(string.Empty, symbol.ExchangeSymbol);
    }

    [Fact]
    public void Candle_round_trips_all_properties()
    {
        var openTime = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var ingestedAt = new DateTimeOffset(2026, 6, 24, 12, 1, 0, TimeSpan.Zero);

        var candle = new Candle
        {
            ExchangeId = 7,
            SymbolId = 42,
            Interval = Timeframe.H1,
            OpenTime = openTime,
            Open = 100.5m,
            High = 110m,
            Low = 99m,
            Close = 105.25m,
            Volume = 12.5m,
            QuoteVolume = 1312.5m,
            TradeCount = 321,
            IsClosed = true,
            IngestedAt = ingestedAt,
        };

        Assert.Equal(7, candle.ExchangeId);
        Assert.Equal(42, candle.SymbolId);
        Assert.Equal(Timeframe.H1, candle.Interval);
        Assert.Equal(openTime, candle.OpenTime);
        Assert.Equal(100.5m, candle.Open);
        Assert.Equal(110m, candle.High);
        Assert.Equal(99m, candle.Low);
        Assert.Equal(105.25m, candle.Close);
        Assert.Equal(12.5m, candle.Volume);
        Assert.Equal(1312.5m, candle.QuoteVolume);
        Assert.Equal(321, candle.TradeCount);
        Assert.True(candle.IsClosed);
        Assert.Equal(ingestedAt, candle.IngestedAt);
    }

    [Theory]
    [InlineData(Timeframe.M1, 1)]
    [InlineData(Timeframe.M5, 5)]
    [InlineData(Timeframe.M15, 15)]
    [InlineData(Timeframe.H1, 60)]
    [InlineData(Timeframe.H4, 240)]
    [InlineData(Timeframe.D1, 1440)]
    public void Timeframe_has_stable_numeric_codes(Timeframe interval, short expected)
    {
        Assert.Equal(expected, (short)interval);
    }
}
