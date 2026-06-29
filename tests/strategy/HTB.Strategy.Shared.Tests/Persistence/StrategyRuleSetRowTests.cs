using HTB.Strategy.Shared.Domain;
using HTB.Strategy.Shared.Persistence;

namespace HTB.Strategy.Shared.Tests.Persistence;

public sealed class StrategyRuleSetRowTests
{
    private static StrategyVersionId VersionId() =>
        new(new StrategyId("rsi-movement"), new StrategyVersion(1));

    private static StrategyRuleSet Sample() =>
        new(
            VersionId(),
            TradeDirection.LongOnly,
            [new ParameterSpec("oversold", 30m, 20m, 40m, 5m)],
            [new IndicatorSpec("rsi", IndicatorKind.Rsi, Operand.Literal(14m), PriceSource.Close)],
            new SignalRule(LogicalOperator.All, [new Condition(Operand.Indicator("rsi"), ComparisonOperator.LessThan, Operand.Parameter("oversold"))]),
            new SignalRule(LogicalOperator.Any, [new Condition(Operand.Indicator("rsi"), ComparisonOperator.GreaterThan, Operand.Literal(70m))]),
            new RiskRules(new PositionSizing(SizingMethod.PercentEquity, 0.1m), null, null, 1, 1)
        );

    [Fact]
    public void From_captures_the_version_id_and_serialized_rules()
    {
        var row = StrategyRuleSetRow.From(Sample());

        Assert.Equal(VersionId(), row.VersionId);
        Assert.False(string.IsNullOrWhiteSpace(row.Rules));
    }

    [Fact]
    public void To_domain_round_trips_the_aggregate()
    {
        var row = StrategyRuleSetRow.From(Sample());

        var restored = row.ToDomain();

        Assert.Equal(VersionId(), restored.VersionId);
        Assert.Equal(StrategyRuleSetSerializer.Serialize(Sample()), StrategyRuleSetSerializer.Serialize(restored));
    }

    [Fact]
    public void From_rejects_null_rules()
    {
        Assert.Throws<ArgumentNullException>(() => StrategyRuleSetRow.From(null!));
    }
}
