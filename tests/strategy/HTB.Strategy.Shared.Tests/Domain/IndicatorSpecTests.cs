using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class IndicatorSpecTests
{
    [Fact]
    public void Constructor_keeps_its_definition()
    {
        var period = Operand.Parameter("rsiPeriod");
        var spec = new IndicatorSpec("rsi", IndicatorKind.Rsi, period, PriceSource.Close);

        Assert.Equal("rsi", spec.Name);
        Assert.Equal(IndicatorKind.Rsi, spec.Kind);
        Assert.Same(period, spec.Period);
        Assert.Equal(PriceSource.Close, spec.Source);
    }

    [Fact]
    public void Constructor_trims_the_name()
    {
        var spec = new IndicatorSpec("  emaSlow ", IndicatorKind.Ema, Operand.Literal(200m), PriceSource.Close);

        Assert.Equal("emaSlow", spec.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_name(string name)
    {
        Assert.Throws<StrategyDomainException>(
            () => new IndicatorSpec(name, IndicatorKind.Rsi, Operand.Literal(14m), PriceSource.Close)
        );
    }

    [Fact]
    public void Constructor_rejects_null_period()
    {
        Assert.Throws<ArgumentNullException>(
            () => new IndicatorSpec("rsi", IndicatorKind.Rsi, null!, PriceSource.Close)
        );
    }
}
