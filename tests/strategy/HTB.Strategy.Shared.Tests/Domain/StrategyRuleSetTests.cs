using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class StrategyRuleSetTests
{
    private static StrategyVersionId VersionId() =>
        new(new StrategyId("rsi-movement"), new StrategyVersion(1));

    private static ParameterSpec Param(string name) => new(name, 14m, 7m, 21m, 1m);

    private static IndicatorSpec Indicator(string name) =>
        new(name, IndicatorKind.Rsi, Operand.Literal(14m), PriceSource.Close);

    private static SignalRule Signal() =>
        new(LogicalOperator.All, [new Condition(Operand.Indicator("rsi"), ComparisonOperator.LessThan, Operand.Literal(30m))]);

    private static RiskRules Risk() =>
        new(new PositionSizing(SizingMethod.PercentEquity, 0.1m), null, null, 1, 1);

    private static StrategyRuleSet Build(
        IReadOnlyList<ParameterSpec>? parameters = null,
        IReadOnlyList<IndicatorSpec>? indicators = null,
        SignalRule? entry = null,
        SignalRule? exit = null,
        RiskRules? risk = null
    ) =>
        new(
            VersionId(),
            TradeDirection.LongOnly,
            parameters ?? [Param("oversold")],
            indicators ?? [Indicator("rsi")],
            entry ?? Signal(),
            exit ?? Signal(),
            risk ?? Risk()
        );

    [Fact]
    public void Constructor_keeps_every_field()
    {
        var parameters = new[] { Param("oversold") };
        var indicators = new[] { Indicator("rsi") };
        var entry = Signal();
        var exit = Signal();
        var risk = Risk();

        var rules = new StrategyRuleSet(
            VersionId(),
            TradeDirection.LongOnly,
            parameters,
            indicators,
            entry,
            exit,
            risk
        );

        Assert.Equal(VersionId(), rules.VersionId);
        Assert.Equal(TradeDirection.LongOnly, rules.Direction);
        Assert.Equal(parameters, rules.Parameters);
        Assert.Equal(indicators, rules.Indicators);
        Assert.Same(entry, rules.Entry);
        Assert.Same(exit, rules.Exit);
        Assert.Same(risk, rules.Risk);
    }

    [Fact]
    public void Constructor_allows_empty_parameters_and_indicators()
    {
        var rules = Build(parameters: [], indicators: []);

        Assert.Empty(rules.Parameters);
        Assert.Empty(rules.Indicators);
    }

    [Fact]
    public void Constructor_rejects_null_parameters()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StrategyRuleSet(VersionId(), TradeDirection.LongOnly, null!, [Indicator("rsi")], Signal(), Signal(), Risk())
        );
    }

    [Fact]
    public void Constructor_rejects_null_indicators()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StrategyRuleSet(VersionId(), TradeDirection.LongOnly, [Param("oversold")], null!, Signal(), Signal(), Risk())
        );
    }

    [Fact]
    public void Constructor_rejects_null_entry()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StrategyRuleSet(VersionId(), TradeDirection.LongOnly, [Param("oversold")], [Indicator("rsi")], null!, Signal(), Risk())
        );
    }

    [Fact]
    public void Constructor_rejects_null_exit()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StrategyRuleSet(VersionId(), TradeDirection.LongOnly, [Param("oversold")], [Indicator("rsi")], Signal(), null!, Risk())
        );
    }

    [Fact]
    public void Constructor_rejects_null_risk()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StrategyRuleSet(VersionId(), TradeDirection.LongOnly, [Param("oversold")], [Indicator("rsi")], Signal(), Signal(), null!)
        );
    }

    [Fact]
    public void Constructor_rejects_duplicate_parameter_names()
    {
        Assert.Throws<StrategyDomainException>(
            () => Build(parameters: [Param("oversold"), Param("oversold")])
        );
    }

    [Fact]
    public void Constructor_rejects_duplicate_indicator_names()
    {
        Assert.Throws<StrategyDomainException>(
            () => Build(indicators: [Indicator("rsi"), Indicator("rsi")])
        );
    }
}
