using HTB.Shared.Strategy.Domain.Conditions;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class OperandTests
{
    private static EvaluationContext Context() =>
        new(
            Closes(10, 20, 30),
            2,
            new Dictionary<string, IReadOnlyList<decimal>> { ["rsi"] = new decimal[] { 1, 2, 3 } },
            new Dictionary<string, decimal> { ["oversold"] = 25m }
        );

    [Fact]
    public void Literal_returns_its_constant_regardless_of_context()
    {
        var operand = new LiteralOperand(42m);
        Assert.Equal(42m, operand.Value);
        Assert.Equal(42m, operand.Evaluate(Context()));
    }

    [Fact]
    public void Parameter_resolves_its_bound_value()
    {
        var operand = new ParameterOperand("oversold");
        Assert.Equal("oversold", operand.Name);
        Assert.Equal(25m, operand.Evaluate(Context()));
    }

    [Fact]
    public void Parameter_rejects_a_blank_name_and_null_context()
    {
        Assert.Throws<ArgumentException>(() => new ParameterOperand(""));
        Assert.Throws<ArgumentNullException>(() => new ParameterOperand("x").Evaluate(null!));
    }

    [Fact]
    public void Series_reads_a_candle_field_at_offset_zero()
    {
        var operand = new SeriesOperand("close", 0);
        Assert.Equal("close", operand.Name);
        Assert.Equal(0, operand.Offset);
        Assert.Equal(30m, operand.Evaluate(Context()));
    }

    [Fact]
    public void Series_reads_an_indicator_at_an_offset()
    {
        var operand = new SeriesOperand("rsi", 1);
        Assert.Equal(2m, operand.Evaluate(Context()));
    }

    [Fact]
    public void Series_rejects_a_blank_name_and_negative_offset_and_null_context()
    {
        Assert.Throws<ArgumentException>(() => new SeriesOperand("", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeriesOperand("close", -1));
        Assert.Throws<ArgumentNullException>(() => new SeriesOperand("close", 0).Evaluate(null!));
    }
}
