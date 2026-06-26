using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Domain.Conditions;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class ConditionTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<decimal>> NoSeries =
        new Dictionary<string, IReadOnlyList<decimal>>();

    private static readonly IReadOnlyDictionary<string, decimal> NoParams =
        new Dictionary<string, decimal>();

    private static EvaluationContext Context(params decimal[] closes) =>
        new(Closes(closes), closes.Length - 1, NoSeries, NoParams);

    private static ICondition True => new ComparisonCondition(
        ComparisonOperator.Equal,
        new LiteralOperand(1m),
        new LiteralOperand(1m)
    );

    private static ICondition False => new ComparisonCondition(
        ComparisonOperator.Equal,
        new LiteralOperand(1m),
        new LiteralOperand(2m)
    );

    [Fact]
    public void All_is_true_only_when_every_child_is()
    {
        Assert.True(new AllCondition([True, True]).IsSatisfiedBy(Context(1)));
        Assert.False(new AllCondition([True, False]).IsSatisfiedBy(Context(1)));
        Assert.True(new AllCondition([]).IsSatisfiedBy(Context(1))); // vacuously true
    }

    [Fact]
    public void Any_is_true_when_at_least_one_child_is()
    {
        Assert.True(new AnyCondition([False, True]).IsSatisfiedBy(Context(1)));
        Assert.False(new AnyCondition([False, False]).IsSatisfiedBy(Context(1)));
        Assert.False(new AnyCondition([]).IsSatisfiedBy(Context(1))); // vacuously false
    }

    [Fact]
    public void Not_negates_its_child()
    {
        Assert.True(new NotCondition(False).IsSatisfiedBy(Context(1)));
        Assert.False(new NotCondition(True).IsSatisfiedBy(Context(1)));
    }

    [Fact]
    public void Combinators_reject_null_children()
    {
        Assert.Throws<ArgumentNullException>(() => new AllCondition(null!));
        Assert.Throws<ArgumentNullException>(() => new AnyCondition(null!));
        Assert.Throws<ArgumentNullException>(() => new NotCondition(null!));
    }

    [Theory]
    [InlineData(ComparisonOperator.GreaterThan, 30, 20, true)]
    [InlineData(ComparisonOperator.GreaterThan, 20, 20, false)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 20, 20, true)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 10, 20, false)]
    [InlineData(ComparisonOperator.LessThan, 10, 20, true)]
    [InlineData(ComparisonOperator.LessThan, 20, 20, false)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 20, 20, true)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 30, 20, false)]
    [InlineData(ComparisonOperator.Equal, 20, 20, true)]
    [InlineData(ComparisonOperator.Equal, 20, 21, false)]
    public void Comparison_evaluates_each_operator(ComparisonOperator op, int left, int right, bool expected)
    {
        var condition = new ComparisonCondition(op, new LiteralOperand(left), new LiteralOperand(right));
        Assert.Equal(expected, condition.IsSatisfiedBy(Context(1)));
    }

    [Fact]
    public void Comparison_rejects_null_operands_and_an_unknown_operator()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ComparisonCondition(ComparisonOperator.Equal, null!, new LiteralOperand(1m))
        );
        Assert.Throws<ArgumentNullException>(
            () => new ComparisonCondition(ComparisonOperator.Equal, new LiteralOperand(1m), null!)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ComparisonCondition((ComparisonOperator)999, new LiteralOperand(1m), new LiteralOperand(1m))
                .IsSatisfiedBy(Context(1))
        );
    }

    [Fact]
    public void CrossAbove_fires_only_on_the_upward_crossing()
    {
        var cross = new CrossCondition(CrossDirection.Above, new SeriesOperand("close", 0), new LiteralOperand(15m));

        Assert.True(cross.IsSatisfiedBy(Context(10, 20)));  // 10 -> 20 over 15
        Assert.False(cross.IsSatisfiedBy(Context(16, 20))); // already above
        Assert.False(cross.IsSatisfiedBy(Context(20, 10))); // moving down
    }

    [Fact]
    public void CrossBelow_fires_only_on_the_downward_crossing()
    {
        var cross = new CrossCondition(CrossDirection.Below, new SeriesOperand("close", 0), new LiteralOperand(15m));

        Assert.True(cross.IsSatisfiedBy(Context(20, 10)));  // 20 -> 10 under 15
        Assert.False(cross.IsSatisfiedBy(Context(10, 20))); // moving up
    }

    [Fact]
    public void Cross_rejects_null_operands_null_context_and_an_unknown_direction()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CrossCondition(CrossDirection.Above, null!, new LiteralOperand(1m))
        );
        Assert.Throws<ArgumentNullException>(
            () => new CrossCondition(CrossDirection.Above, new LiteralOperand(1m), null!)
        );
        Assert.Throws<ArgumentNullException>(
            () => new CrossCondition(CrossDirection.Above, new LiteralOperand(1m), new LiteralOperand(1m))
                .IsSatisfiedBy(null!)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CrossCondition((CrossDirection)999, new SeriesOperand("close", 0), new LiteralOperand(1m))
                .IsSatisfiedBy(Context(10, 20))
        );
    }
}
