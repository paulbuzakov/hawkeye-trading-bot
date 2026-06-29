using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class OperandTests
{
    [Fact]
    public void Literal_carries_a_numeric_value()
    {
        var operand = Operand.Literal(30m);

        Assert.Equal(OperandKind.Literal, operand.Kind);
        Assert.Equal(30m, operand.Number);
        Assert.Null(operand.Name);
        Assert.Null(operand.Field);
    }

    [Fact]
    public void Parameter_carries_a_trimmed_reference_name()
    {
        var operand = Operand.Parameter("  oversold ");

        Assert.Equal(OperandKind.Parameter, operand.Kind);
        Assert.Equal("oversold", operand.Name);
        Assert.Null(operand.Number);
        Assert.Null(operand.Field);
    }

    [Fact]
    public void Indicator_carries_a_trimmed_reference_name()
    {
        var operand = Operand.Indicator("  emaSlow ");

        Assert.Equal(OperandKind.Indicator, operand.Kind);
        Assert.Equal("emaSlow", operand.Name);
        Assert.Null(operand.Number);
        Assert.Null(operand.Field);
    }

    [Fact]
    public void Price_carries_a_bar_field()
    {
        var operand = Operand.Price(PriceSource.Close);

        Assert.Equal(OperandKind.PriceField, operand.Kind);
        Assert.Equal(PriceSource.Close, operand.Field);
        Assert.Null(operand.Number);
        Assert.Null(operand.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parameter_rejects_blank_name(string name)
    {
        Assert.Throws<StrategyDomainException>(() => Operand.Parameter(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Indicator_rejects_blank_name(string name)
    {
        Assert.Throws<StrategyDomainException>(() => Operand.Indicator(name));
    }
}
