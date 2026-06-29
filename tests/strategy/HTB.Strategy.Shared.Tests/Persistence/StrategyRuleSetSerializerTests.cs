using HTB.Strategy.Shared.Domain;
using HTB.Strategy.Shared.Persistence;

namespace HTB.Strategy.Shared.Tests.Persistence;

public sealed class StrategyRuleSetSerializerTests
{
    private static StrategyVersionId VersionId() =>
        new(new StrategyId("rsi-movement"), new StrategyVersion(1));

    // A rule set exercising every operand kind (literal, parameter, indicator, price field),
    // both signal modes, and a fully-populated risk block.
    private static StrategyRuleSet Sample(ProtectiveExit? stop = null, ProtectiveExit? take = null) =>
        new(
            VersionId(),
            TradeDirection.LongOnly,
            [
                new ParameterSpec("rsiPeriod", 14m, 7m, 21m, 1m),
                new ParameterSpec("oversold", 30m, 20m, 40m, 5m),
            ],
            [
                new IndicatorSpec("rsi", IndicatorKind.Rsi, Operand.Parameter("rsiPeriod"), PriceSource.Close),
                new IndicatorSpec("emaSlow", IndicatorKind.Ema, Operand.Literal(200m), PriceSource.Close),
            ],
            new SignalRule(
                LogicalOperator.All,
                [
                    new Condition(Operand.Indicator("rsi"), ComparisonOperator.LessThan, Operand.Parameter("oversold")),
                    new Condition(Operand.Price(PriceSource.Close), ComparisonOperator.GreaterThan, Operand.Indicator("emaSlow")),
                ]
            ),
            new SignalRule(
                LogicalOperator.Any,
                [new Condition(Operand.Indicator("rsi"), ComparisonOperator.GreaterThan, Operand.Literal(70m))]
            ),
            new RiskRules(new PositionSizing(SizingMethod.PercentEquity, 0.1m), stop, take, 1, 1)
        );

    [Fact]
    public void Round_trip_is_stable()
    {
        var stop = new ProtectiveExit(BracketType.Percent, 0.05m);
        var take = new ProtectiveExit(BracketType.Percent, 0.10m);
        var json = StrategyRuleSetSerializer.Serialize(Sample(stop, take));

        var restored = StrategyRuleSetSerializer.Deserialize(VersionId(), json);

        Assert.Equal(json, StrategyRuleSetSerializer.Serialize(restored));
    }

    [Fact]
    public void Deserialize_restores_the_version_id_from_the_argument()
    {
        var json = StrategyRuleSetSerializer.Serialize(Sample());

        var restored = StrategyRuleSetSerializer.Deserialize(VersionId(), json);

        Assert.Equal(VersionId(), restored.VersionId);
    }

    [Fact]
    public void Deserialize_restores_the_rule_tree()
    {
        var stop = new ProtectiveExit(BracketType.Percent, 0.05m);
        var json = StrategyRuleSetSerializer.Serialize(Sample(stop, take: null));

        var r = StrategyRuleSetSerializer.Deserialize(VersionId(), json);

        Assert.Equal(TradeDirection.LongOnly, r.Direction);
        Assert.Equal(2, r.Parameters.Count);
        Assert.Equal("rsiPeriod", r.Parameters[0].Name);
        Assert.Equal(OperandKind.Parameter, r.Indicators[0].Period.Kind);
        Assert.Equal("rsiPeriod", r.Indicators[0].Period.Name);
        Assert.Equal(LogicalOperator.All, r.Entry.Mode);
        Assert.Equal(OperandKind.PriceField, r.Entry.Conditions[1].Left.Kind);
        Assert.Equal(PriceSource.Close, r.Entry.Conditions[1].Left.Field);
        Assert.Equal(LogicalOperator.Any, r.Exit.Mode);
        Assert.Equal(70m, r.Exit.Conditions[0].Right.Number);
        Assert.Equal(BracketType.Percent, r.Risk.StopLoss!.Type);
        Assert.Null(r.Risk.TakeProfit);
        Assert.Equal(1, r.Risk.MaxOpenPositions);
    }

    [Fact]
    public void Serialize_rejects_null_rules()
    {
        Assert.Throws<ArgumentNullException>(() => StrategyRuleSetSerializer.Serialize(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_rejects_blank_json(string json)
    {
        Assert.Throws<ArgumentException>(() => StrategyRuleSetSerializer.Deserialize(VersionId(), json));
    }

    [Fact]
    public void Deserialize_rejects_malformed_json()
    {
        Assert.Throws<StrategyDomainException>(
            () => StrategyRuleSetSerializer.Deserialize(VersionId(), "{ not json ")
        );
    }

    [Fact]
    public void Deserialize_rejects_a_null_json_literal()
    {
        Assert.Throws<StrategyDomainException>(
            () => StrategyRuleSetSerializer.Deserialize(VersionId(), "null")
        );
    }
}
