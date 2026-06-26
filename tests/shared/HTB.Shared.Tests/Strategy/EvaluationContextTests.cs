using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Domain.Conditions;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class EvaluationContextTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<decimal>> NoSeries =
        new Dictionary<string, IReadOnlyList<decimal>>();

    private static readonly IReadOnlyDictionary<string, decimal> NoParams =
        new Dictionary<string, decimal>();

    private static EvaluationContext Context(
        IReadOnlyList<Candle> window,
        int decisionIndex,
        IReadOnlyDictionary<string, IReadOnlyList<decimal>>? series = null,
        IReadOnlyDictionary<string, decimal>? parameters = null
    ) => new(window, decisionIndex, series ?? NoSeries, parameters ?? NoParams);

    [Fact]
    public void Ctor_rejects_null_arguments()
    {
        var window = Closes(1);
        Assert.Throws<ArgumentNullException>(() => new EvaluationContext(null!, 0, NoSeries, NoParams));
        Assert.Throws<ArgumentNullException>(() => new EvaluationContext(window, 0, null!, NoParams));
        Assert.Throws<ArgumentNullException>(() => new EvaluationContext(window, 0, NoSeries, null!));
    }

    [Fact]
    public void Ctor_rejects_an_empty_window()
    {
        Assert.Throws<ArgumentException>(() => Context([], 0));
    }

    [Fact]
    public void Ctor_rejects_a_decision_index_past_the_window()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Context(Closes(1, 2), 2));
    }

    [Fact]
    public void GetSeries_reads_candle_fields_at_an_offset()
    {
        var context = Context(Closes(10, 20, 30), 2);

        Assert.Equal(30m, context.GetSeries("close", 0));
        Assert.Equal(20m, context.GetSeries("close", 1));
        Assert.Equal(10m, context.GetSeries("close", 2));
    }

    [Fact]
    public void GetSeries_reads_indicator_series()
    {
        var series = new Dictionary<string, IReadOnlyList<decimal>>
        {
            ["rsi"] = new decimal[] { 40, 50, 60 },
        };
        var context = Context(Closes(1, 2, 3), 2, series);

        Assert.Equal(60m, context.GetSeries("rsi", 0));
        Assert.Equal(50m, context.GetSeries("rsi", 1));
    }

    [Fact]
    public void GetSeries_rejects_a_blank_name()
    {
        var context = Context(Closes(1), 0);
        Assert.Throws<ArgumentException>(() => context.GetSeries("", 0));
    }

    [Fact]
    public void GetSeries_rejects_a_negative_offset()
    {
        var context = Context(Closes(1), 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => context.GetSeries("close", -1));
    }

    [Fact]
    public void GetSeries_throws_when_history_is_insufficient()
    {
        var context = Context(Closes(1, 2), 1);
        Assert.Throws<InvalidOperationException>(() => context.GetSeries("close", 5));
    }

    [Fact]
    public void GetSeries_throws_for_an_unknown_series()
    {
        var context = Context(Closes(1), 0);
        Assert.Throws<KeyNotFoundException>(() => context.GetSeries("rsi", 0));
    }

    [Fact]
    public void GetParameter_resolves_declared_values()
    {
        var parameters = new Dictionary<string, decimal> { ["rsi-oversold"] = 30m };
        var context = Context(Closes(1), 0, parameters: parameters);

        Assert.Equal(30m, context.GetParameter("rsi-oversold"));
    }

    [Fact]
    public void GetParameter_rejects_a_blank_name_and_unknown_names()
    {
        var context = Context(Closes(1), 0);
        Assert.Throws<ArgumentException>(() => context.GetParameter(""));
        Assert.Throws<KeyNotFoundException>(() => context.GetParameter("missing"));
    }

    [Fact]
    public void ShiftBack_moves_the_decision_point_earlier()
    {
        var context = Context(Closes(10, 20, 30), 2);
        var previous = context.ShiftBack(1);

        Assert.Equal(1, previous.DecisionIndex);
        Assert.Equal(20m, previous.GetSeries("close", 0));
    }

    [Fact]
    public void ShiftBack_rejects_a_negative_shift()
    {
        var context = Context(Closes(1), 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => context.ShiftBack(-1));
    }
}
