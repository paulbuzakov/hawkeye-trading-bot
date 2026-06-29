using System.Text.Json;
using HTB.Strategy.Shared.Domain;
using HTB.Strategy.Shared.Persistence;

namespace HTB.Strategy.Shared.Tests.Persistence;

public sealed class OperandJsonConverterTests
{
    private static readonly JsonSerializerOptions Options =
        new() { Converters = { new OperandJsonConverter() } };

    private static Operand RoundTrip(Operand operand)
    {
        var json = JsonSerializer.Serialize(operand, Options);
        return JsonSerializer.Deserialize<Operand>(json, Options)!;
    }

    [Fact]
    public void Round_trips_a_literal()
    {
        var result = RoundTrip(Operand.Literal(30m));

        Assert.Equal(OperandKind.Literal, result.Kind);
        Assert.Equal(30m, result.Number);
    }

    [Fact]
    public void Round_trips_a_parameter_reference()
    {
        var result = RoundTrip(Operand.Parameter("oversold"));

        Assert.Equal(OperandKind.Parameter, result.Kind);
        Assert.Equal("oversold", result.Name);
    }

    [Fact]
    public void Round_trips_an_indicator_reference()
    {
        var result = RoundTrip(Operand.Indicator("emaSlow"));

        Assert.Equal(OperandKind.Indicator, result.Kind);
        Assert.Equal("emaSlow", result.Name);
    }

    [Fact]
    public void Round_trips_a_price_field()
    {
        var result = RoundTrip(Operand.Price(PriceSource.Close));

        Assert.Equal(OperandKind.PriceField, result.Kind);
        Assert.Equal(PriceSource.Close, result.Field);
    }

    [Fact]
    public void Read_rejects_an_unknown_operand_kind()
    {
        Assert.Throws<StrategyDomainException>(
            () => JsonSerializer.Deserialize<Operand>("""{"kind":99}""", Options)
        );
    }
}
