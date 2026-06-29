using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class ConditionTests
{
    [Fact]
    public void Constructor_keeps_both_operands_and_the_operator()
    {
        var left = Operand.Indicator("rsi");
        var right = Operand.Parameter("oversold");
        var condition = new Condition(left, ComparisonOperator.LessThan, right);

        Assert.Same(left, condition.Left);
        Assert.Equal(ComparisonOperator.LessThan, condition.Operator);
        Assert.Same(right, condition.Right);
    }

    [Fact]
    public void Constructor_rejects_null_left()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Condition(null!, ComparisonOperator.LessThan, Operand.Literal(1m))
        );
    }

    [Fact]
    public void Constructor_rejects_null_right()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Condition(Operand.Literal(1m), ComparisonOperator.LessThan, null!)
        );
    }
}
