using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class SignalRuleTests
{
    private static Condition AnyCondition() =>
        new(Operand.Indicator("rsi"), ComparisonOperator.LessThan, Operand.Parameter("oversold"));

    [Fact]
    public void Constructor_keeps_mode_and_conditions()
    {
        var conditions = new[] { AnyCondition() };
        var rule = new SignalRule(LogicalOperator.All, conditions);

        Assert.Equal(LogicalOperator.All, rule.Mode);
        Assert.Equal(conditions, rule.Conditions);
    }

    [Fact]
    public void Constructor_rejects_null_conditions()
    {
        Assert.Throws<ArgumentNullException>(() => new SignalRule(LogicalOperator.Any, null!));
    }

    [Fact]
    public void Constructor_rejects_empty_conditions()
    {
        Assert.Throws<StrategyDomainException>(() => new SignalRule(LogicalOperator.Any, []));
    }
}
