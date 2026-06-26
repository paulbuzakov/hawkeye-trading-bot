using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Domain.Conditions;
using HTB.Shared.Strategy.Strategy;
using HTB.Shared.Strategy.Strategy.Indicators;

namespace HTB.Shared.Tests.Strategy;

public class StrategyDomainTests
{
    [Theory]
    [InlineData(StrategyStatus.Draft, (short)1)]
    [InlineData(StrategyStatus.Active, (short)2)]
    [InlineData(StrategyStatus.Retired, (short)3)]
    public void StrategyStatus_has_stable_codes(StrategyStatus status, short code) =>
        Assert.Equal(code, (short)status);

    [Theory]
    [InlineData(Signal.Hold, (short)0)]
    [InlineData(Signal.OpenLong, (short)1)]
    [InlineData(Signal.CloseLong, (short)2)]
    [InlineData(Signal.OpenShort, (short)3)]
    [InlineData(Signal.CloseShort, (short)4)]
    public void Signal_has_stable_codes(Signal signal, short code) => Assert.Equal(code, (short)signal);

    [Fact]
    public void Domain_records_use_value_equality()
    {
        var version = new StrategyVersion(1, StrategyStatus.Active, default, "sha256:x");
        Assert.Equal(version, new StrategyVersion(1, StrategyStatus.Active, default, "sha256:x"));

        var spec = new ParameterSpec(ParameterType.Int, 14, 2, 50);
        Assert.Equal(spec, new ParameterSpec(ParameterType.Int, 14, 2, 50));

        var risk = new RequestedRisk(2m, 4m, 10m);
        Assert.Equal(risk, new RequestedRisk(2m, 4m, 10m));

        var execution = new ExecutionHints(OrderType.Market, TimeInForce.Gtc, 0.1m);
        Assert.Equal(execution, new ExecutionHints(OrderType.Market, TimeInForce.Gtc, 0.1m));

        var indicator = new IndicatorSpec("rsi", "rsi", CandleField.Close, 14);
        Assert.Equal(indicator, new IndicatorSpec("rsi", "rsi", CandleField.Close, 14));
    }

    [Fact]
    public void Manifest_rules_and_definition_round_trip_their_members()
    {
        var version = new StrategyVersion(1, StrategyStatus.Active, default, "sha256:x");
        var applicability = new Applicability(["binance"], ["BTCUSDT"], Timeframe.H1, 200);
        var parameters = new Dictionary<string, ParameterSpec> { ["p"] = new(ParameterType.Int, 1, 1, 1) };
        var manifest = new StrategyManifest("1.0", "id", "Name", "desc", "paul", true, version, applicability, parameters, ["tag"]);

        Assert.Equal("1.0", manifest.SchemaVersion);
        Assert.Equal("id", manifest.Id);
        Assert.Equal("Name", manifest.Name);
        Assert.Equal("desc", manifest.Description);
        Assert.Equal("paul", manifest.Author);
        Assert.True(manifest.Deterministic);
        Assert.Equal(applicability, manifest.Applicability);
        Assert.Equal(version, manifest.Version);
        Assert.Equal("binance", manifest.Applicability.Exchanges[0]);
        Assert.Equal("tag", manifest.Tags[0]);
        Assert.Equal(default, version.CreatedAt);
        Assert.Equal("sha256:x", version.RulesHash);

        ICondition condition = new ComparisonCondition(ComparisonOperator.Equal, new LiteralOperand(1m), new LiteralOperand(1m));
        var rules = new StrategyRules(
            "1.0",
            "id",
            1,
            [new IndicatorSpec("rsi", "rsi", CandleField.Close, 14)],
            new Dictionary<string, ICondition> { ["entry-long"] = condition },
            new RequestedRisk(2m, 4m, 10m),
            new ExecutionHints(OrderType.Market, TimeInForce.Gtc, 0.1m)
        );
        Assert.Equal("1.0", rules.SchemaVersion);
        Assert.Equal("id", rules.StrategyId);
        Assert.Equal(1, rules.VersionNumber);
        Assert.Single(rules.Indicators);
        Assert.Single(rules.Rules);
        Assert.NotNull(rules.RequestedRisk);
        Assert.NotNull(rules.Execution);

        var values = new Dictionary<string, decimal> { ["p"] = 1m };
        var definition = new StrategyDefinition(manifest, rules, values, true);
        Assert.Same(manifest, definition.Manifest);
        Assert.Same(rules, definition.Rules);
        Assert.True(definition.IsRunnable);
        Assert.Equal(1m, definition.Parameters["p"]);
    }

    [Theory]
    [InlineData("rsi")]
    [InlineData("ema")]
    [InlineData("sma")]
    public void IndicatorFactory_creates_each_known_kind(string kind)
    {
        Assert.True(IndicatorFactory.IsKnown(kind));
        var indicator = IndicatorFactory.Create(new IndicatorSpec("i", kind, CandleField.Close, 3));
        Assert.NotNull(indicator);
    }

    [Fact]
    public void IndicatorFactory_rejects_null_and_unknown_kinds()
    {
        Assert.False(IndicatorFactory.IsKnown("macd"));
        Assert.Throws<ArgumentNullException>(() => IndicatorFactory.Create(null!));
        Assert.Throws<StrategyConfigException>(
            () => IndicatorFactory.Create(new IndicatorSpec("i", "macd", CandleField.Close, 3))
        );
    }
}
