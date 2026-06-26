using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Domain.Conditions;
using HTB.Shared.Strategy.Strategy;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class DeclarativeStrategyTests
{
    private static ICondition CloseEquals(decimal value) =>
        new ComparisonCondition(ComparisonOperator.Equal, new SeriesOperand("close", 0), new LiteralOperand(value));

    private static StrategyDefinition Definition(
        IReadOnlyDictionary<string, ICondition> rules,
        IReadOnlyList<IndicatorSpec>? indicators = null,
        IReadOnlyDictionary<string, decimal>? parameters = null,
        bool isRunnable = true
    )
    {
        var manifest = new StrategyManifest(
            "1.0",
            "s",
            "S",
            "",
            "",
            true,
            new StrategyVersion(1, isRunnable ? StrategyStatus.Active : StrategyStatus.Draft, default, "sha256:x"),
            new Applicability([], [], Timeframe.H1, 0),
            new Dictionary<string, ParameterSpec>(),
            []
        );
        var strategyRules = new StrategyRules("1.0", "s", 1, indicators ?? [], rules, null, null);
        return new StrategyDefinition(manifest, strategyRules, parameters ?? new Dictionary<string, decimal>(), isRunnable);
    }

    [Fact]
    public void Ctor_rejects_null_and_non_runnable_definitions()
    {
        Assert.Throws<ArgumentNullException>(() => new DeclarativeStrategy(null!));

        var draft = Definition(new Dictionary<string, ICondition>(), isRunnable: false);
        Assert.Throws<InvalidOperationException>(() => new DeclarativeStrategy(draft));
    }

    [Fact]
    public void Evaluate_rejects_a_null_context_and_null_window()
    {
        var strategy = new DeclarativeStrategy(Definition(new Dictionary<string, ICondition> { ["entry-long"] = CloseEquals(1) }));
        Assert.Throws<ArgumentNullException>(() => strategy.Evaluate((EvaluationContext)null!));
        Assert.Throws<ArgumentNullException>(() => strategy.Evaluate((IReadOnlyList<Candle>)null!));
    }

    [Theory]
    [InlineData(1, Signal.CloseLong)]
    [InlineData(2, Signal.CloseShort)]
    [InlineData(3, Signal.OpenLong)]
    [InlineData(4, Signal.OpenShort)]
    [InlineData(5, Signal.Hold)]
    public void Evaluate_maps_each_rule_to_its_signal(int close, Signal expected)
    {
        var rules = new Dictionary<string, ICondition>
        {
            ["exit-long"] = CloseEquals(1),
            ["exit-short"] = CloseEquals(2),
            ["entry-long"] = CloseEquals(3),
            ["entry-short"] = CloseEquals(4),
        };
        var strategy = new DeclarativeStrategy(Definition(rules));

        Assert.Equal(expected, strategy.Evaluate(Closes(close)));
    }

    [Fact]
    public void Evaluate_prefers_exit_over_entry_when_both_fire()
    {
        var rules = new Dictionary<string, ICondition>
        {
            ["entry-long"] = CloseEquals(1),
            ["exit-long"] = CloseEquals(1),
        };
        var strategy = new DeclarativeStrategy(Definition(rules));

        Assert.Equal(Signal.CloseLong, strategy.Evaluate(Closes(1)));
    }

    [Fact]
    public void Evaluate_window_builds_indicator_series_for_the_rules()
    {
        var indicators = new[] { new IndicatorSpec("avg", "sma", CandleField.Close, 2) };
        var rules = new Dictionary<string, ICondition>
        {
            ["entry-long"] = new ComparisonCondition(
                ComparisonOperator.GreaterThan,
                new SeriesOperand("avg", 0),
                new LiteralOperand(0m)
            ),
        };
        var strategy = new DeclarativeStrategy(Definition(rules, indicators));

        Assert.Equal(Signal.OpenLong, strategy.Evaluate(Closes(10, 10)));
    }

    [Fact]
    public void BuildContext_rejects_an_empty_window()
    {
        var strategy = new DeclarativeStrategy(Definition(new Dictionary<string, ICondition> { ["entry-long"] = CloseEquals(1) }));
        Assert.Throws<ArgumentException>(() => strategy.BuildContext([]));
    }
}
