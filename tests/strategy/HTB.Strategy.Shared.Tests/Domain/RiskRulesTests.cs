using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class PositionSizingTests
{
    [Fact]
    public void Constructor_keeps_method_and_value()
    {
        var sizing = new PositionSizing(SizingMethod.PercentEquity, 0.1m);

        Assert.Equal(SizingMethod.PercentEquity, sizing.Method);
        Assert.Equal(0.1m, sizing.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    public void Constructor_rejects_non_positive_value(decimal value)
    {
        Assert.Throws<StrategyDomainException>(() => new PositionSizing(SizingMethod.PercentEquity, value));
    }
}

public sealed class ProtectiveExitTests
{
    [Fact]
    public void Constructor_keeps_type_and_value()
    {
        var exit = new ProtectiveExit(BracketType.Percent, 0.05m);

        Assert.Equal(BracketType.Percent, exit.Type);
        Assert.Equal(0.05m, exit.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_value(decimal value)
    {
        Assert.Throws<StrategyDomainException>(() => new ProtectiveExit(BracketType.Percent, value));
    }
}

public sealed class RiskRulesTests
{
    private static PositionSizing Sizing() => new(SizingMethod.PercentEquity, 0.1m);

    [Fact]
    public void Constructor_keeps_every_field()
    {
        var sizing = Sizing();
        var stop = new ProtectiveExit(BracketType.Percent, 0.05m);
        var take = new ProtectiveExit(BracketType.Percent, 0.10m);

        var risk = new RiskRules(sizing, stop, take, maxOpenPositions: 1, maxOpenPerSymbol: 1);

        Assert.Same(sizing, risk.PositionSizing);
        Assert.Same(stop, risk.StopLoss);
        Assert.Same(take, risk.TakeProfit);
        Assert.Equal(1, risk.MaxOpenPositions);
        Assert.Equal(1, risk.MaxOpenPerSymbol);
    }

    [Fact]
    public void Constructor_allows_omitting_stop_and_take_profit()
    {
        var risk = new RiskRules(Sizing(), stopLoss: null, takeProfit: null, maxOpenPositions: 2, maxOpenPerSymbol: 1);

        Assert.Null(risk.StopLoss);
        Assert.Null(risk.TakeProfit);
    }

    [Fact]
    public void Constructor_rejects_null_sizing()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RiskRules(null!, stopLoss: null, takeProfit: null, maxOpenPositions: 1, maxOpenPerSymbol: 1)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_max_open_positions(int max)
    {
        Assert.Throws<StrategyDomainException>(
            () => new RiskRules(Sizing(), null, null, maxOpenPositions: max, maxOpenPerSymbol: 1)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_max_open_per_symbol(int max)
    {
        Assert.Throws<StrategyDomainException>(
            () => new RiskRules(Sizing(), null, null, maxOpenPositions: 1, maxOpenPerSymbol: max)
        );
    }
}
