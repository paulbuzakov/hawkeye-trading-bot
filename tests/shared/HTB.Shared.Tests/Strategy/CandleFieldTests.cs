using HTB.Shared.Strategy.Domain;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class CandleFieldTests
{
    [Theory]
    [InlineData("open", CandleField.Open)]
    [InlineData("HIGH", CandleField.High)]
    [InlineData("Low", CandleField.Low)]
    [InlineData("close", CandleField.Close)]
    [InlineData("volume", CandleField.Volume)]
    public void TryParse_accepts_field_names_case_insensitively(string name, CandleField expected)
    {
        Assert.True(CandleFields.TryParse(name, out var field));
        Assert.Equal(expected, field);
    }

    [Fact]
    public void TryParse_rejects_unknown_names()
    {
        Assert.False(CandleFields.TryParse("vwap", out _));
    }

    [Fact]
    public void ValueOf_reads_each_field()
    {
        var candle = Candle(0);
        candle.Open = 1;
        candle.High = 2;
        candle.Low = 3;
        candle.Close = 4;
        candle.Volume = 5;

        Assert.Equal(1m, CandleFields.ValueOf(CandleField.Open, candle));
        Assert.Equal(2m, CandleFields.ValueOf(CandleField.High, candle));
        Assert.Equal(3m, CandleFields.ValueOf(CandleField.Low, candle));
        Assert.Equal(4m, CandleFields.ValueOf(CandleField.Close, candle));
        Assert.Equal(5m, CandleFields.ValueOf(CandleField.Volume, candle));
    }

    [Fact]
    public void ValueOf_rejects_null_candle()
    {
        Assert.Throws<ArgumentNullException>(() => CandleFields.ValueOf(CandleField.Close, null!));
    }

    [Fact]
    public void ValueOf_rejects_an_undefined_field()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CandleFields.ValueOf((CandleField)999, Candle(1))
        );
    }
}
