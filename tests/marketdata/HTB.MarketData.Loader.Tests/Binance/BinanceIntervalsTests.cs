using HTB.MarketData.Loader.Binance;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Tests.Binance;

public class BinanceIntervalsTests
{
    [Theory]
    [InlineData(Timeframe.M1, "1m")]
    [InlineData(Timeframe.M5, "5m")]
    [InlineData(Timeframe.M15, "15m")]
    [InlineData(Timeframe.H1, "1h")]
    [InlineData(Timeframe.H4, "4h")]
    [InlineData(Timeframe.D1, "1d")]
    public void ToCode_maps_every_timeframe(Timeframe timeframe, string expected)
    {
        Assert.Equal(expected, BinanceIntervals.ToCode(timeframe));
    }

    [Fact]
    public void ToCode_unknown_timeframe_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BinanceIntervals.ToCode((Timeframe)999));
    }
}
