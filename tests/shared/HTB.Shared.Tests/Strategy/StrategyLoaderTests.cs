using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Strategy;
using static HTB.Shared.Tests.Strategy.StrategyTestData;

namespace HTB.Shared.Tests.Strategy;

public class StrategyLoaderTests
{
    private static readonly StrategyLoader Loader = new();

    private static StrategyDefinition Load(Dictionary<string, object?> meta, Dictionary<string, object?> rules) =>
        Loader.Load(Json(meta), Json(rules));

    private static void AssertInvalid(Dictionary<string, object?> meta, Dictionary<string, object?> rules) =>
        Assert.Throws<StrategyConfigException>(() => Load(meta, rules));

    private static Dictionary<string, object?> D(object? o) => (Dictionary<string, object?>)o!;

    // ---- happy paths -----------------------------------------------------

    [Fact]
    public void Loads_a_valid_draft_into_a_non_runnable_definition()
    {
        var def = Load(ValidMeta(), ValidRules());

        Assert.False(def.IsRunnable);
        Assert.Equal("rsi-movement", def.Manifest.Id);
        Assert.Equal(StrategyStatus.Draft, def.Manifest.Version.Status);
        Assert.Equal(Timeframe.H1, def.Manifest.Applicability.Timeframe);
        Assert.Equal(200, def.Manifest.Applicability.WarmupBars);

        Assert.Equal(14m, def.Parameters["rsi-period"]);
        Assert.Equal(200m, def.Parameters["ema-period"]);

        Assert.Equal(2, def.Rules.Indicators.Count);
        Assert.Equal(14, def.Rules.Indicators[0].Period); // $rsi-period resolved
        Assert.Equal(200, def.Rules.Indicators[1].Period); // $ema-period resolved
        Assert.Equal(CandleField.Close, def.Rules.Indicators[0].Source);

        Assert.True(def.Rules.Rules.ContainsKey("entry-long"));
        Assert.True(def.Rules.Rules.ContainsKey("exit-long"));

        Assert.Equal(2.0m, def.Rules.RequestedRisk!.StopLossPct);
        Assert.Equal(OrderType.Market, def.Rules.Execution!.OrderType);
        Assert.Equal(TimeInForce.Gtc, def.Rules.Execution.TimeInForce);
    }

    [Fact]
    public void Active_version_with_a_matching_hash_is_runnable()
    {
        var rules = ValidRules();
        var rulesJson = Json(rules);

        var meta = ValidMeta();
        D(meta["version"])["status"] = "active";
        D(meta["version"])["rules-hash"] = HashOf(rulesJson);

        var def = Loader.Load(Json(meta), rulesJson);

        Assert.True(def.IsRunnable);
        Assert.Equal(StrategyStatus.Active, def.Manifest.Version.Status);
    }

    // ---- Validate (non-throwing check step) ------------------------------

    [Fact]
    public void Validate_passes_a_draft_as_valid_but_not_runnable()
    {
        var result = Loader.Validate(Json(ValidMeta()), Json(ValidRules()));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.False(result.IsRunnable); // draft loads but must not run
        Assert.Equal("rsi-movement", result.Definition!.Manifest.Id);
    }

    [Fact]
    public void Validate_passes_an_active_hash_verified_strategy_as_runnable()
    {
        var rulesJson = Json(ValidRules());
        var meta = ValidMeta();
        D(meta["version"])["status"] = "active";
        D(meta["version"])["rules-hash"] = HashOf(rulesJson);

        var result = Loader.Validate(Json(meta), rulesJson);

        Assert.True(result.IsValid);
        Assert.True(result.IsRunnable);
    }

    [Fact]
    public void Validate_returns_an_invalid_verdict_instead_of_throwing()
    {
        var badMeta = ValidMeta();
        D(badMeta["version"])["status"] = "active"; // keeps the all-zero placeholder hash → mismatch

        var result = Loader.Validate(Json(badMeta), Json(ValidRules()));

        Assert.False(result.IsValid);
        Assert.Null(result.Definition);
        Assert.False(result.IsRunnable);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_null_arguments_still_throw_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => Loader.Validate(null!, Json(ValidRules())));
        Assert.Throws<ArgumentNullException>(() => Loader.Validate(Json(ValidMeta()), null!));
    }

    [Fact]
    public void Validation_result_factories_guard_their_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => StrategyValidationResult.Valid(null!));
        Assert.Throws<ArgumentException>(() => StrategyValidationResult.Invalid(" "));
    }

    [Fact]
    public void Active_version_with_a_mismatched_hash_is_rejected()
    {
        var meta = ValidMeta();
        D(meta["version"])["status"] = "active"; // keeps the all-zero placeholder hash
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Compiles_every_dsl_operator_and_operand_form()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = new Dictionary<string, object?>
        {
            ["all"] = new object[]
            {
                Cmp("gt", "close", 1),
                Cmp("gte", "rsi", 1),
                Cmp("lt", "close", 1000000),
                Cmp("lte", "ema-slow", 1000000),
                Cmp("eq", "$rsi-oversold", 30),
                new Dictionary<string, object?>
                {
                    ["crosses-above"] = new object[]
                    {
                        new Dictionary<string, object?> { ["series"] = "close" },
                        new Dictionary<string, object?> { ["series"] = "rsi", ["offset"] = 1 },
                    },
                },
                new Dictionary<string, object?>
                {
                    ["not"] = new Dictionary<string, object?> { ["any"] = new object[] { Cmp("gt", "close", 1000000) } },
                },
            },
        };

        var def = Load(ValidMeta(), rules);
        Assert.True(def.Rules.Rules.ContainsKey("entry-long"));
    }

    [Fact]
    public void Loads_a_minimal_strategy_with_no_parameters_indicators_or_advisories()
    {
        var meta = new Dictionary<string, object?>
        {
            ["schema-version"] = "1.0",
            ["id"] = "bare",
            ["name"] = "Bare",
            ["deterministic"] = true,
            ["version"] = new Dictionary<string, object?>
            {
                ["number"] = 1,
                ["status"] = "draft",
                ["created-at"] = "2026-06-26T00:00:00Z",
                ["rules-hash"] = "sha256:" + new string('0', 64),
            },
            ["applicability"] = new Dictionary<string, object?>
            {
                ["exchanges"] = new[] { "binance" },
                ["symbols"] = new[] { "BTCUSDT" },
                ["timeframe"] = "H1",
                ["warmup-bars"] = 0,
            },
        };
        var rules = new Dictionary<string, object?>
        {
            ["schema-version"] = "1.0",
            ["strategy-id"] = "bare",
            ["version-number"] = 1,
            ["rules"] = new Dictionary<string, object?> { ["entry-long"] = Cmp("gt", "close", 100) },
        };

        var def = Load(meta, rules);

        Assert.Empty(def.Parameters);
        Assert.Empty(def.Rules.Indicators);
        Assert.Null(def.Rules.RequestedRisk);
        Assert.Null(def.Rules.Execution);
        Assert.Empty(def.Manifest.Tags);
    }

    [Fact]
    public void Resolves_a_literal_indicator_period()
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["period"] = 9;

        var def = Load(ValidMeta(), rules);
        Assert.Equal(9, def.Rules.Indicators[0].Period);
    }

    // ---- malformed json --------------------------------------------------

    [Fact]
    public void Rejects_invalid_json()
    {
        Assert.Throws<StrategyConfigException>(() => Loader.Load("{ not json", Json(ValidRules())));
        Assert.Throws<StrategyConfigException>(() => Loader.Load(Json(ValidMeta()), "{ not json"));
    }

    [Fact]
    public void Rejects_a_json_null_document()
    {
        Assert.Throws<StrategyConfigException>(() => Loader.Load("null", Json(ValidRules())));
        Assert.Throws<StrategyConfigException>(() => Loader.Load(Json(ValidMeta()), "null"));
    }

    // ---- manifest validation --------------------------------------------

    [Fact]
    public void Rejects_an_unsupported_schema_version()
    {
        var meta = ValidMeta();
        meta["schema-version"] = "2.0";
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Rejects_a_missing_id_or_name()
    {
        var noId = ValidMeta();
        noId["id"] = "";
        AssertInvalid(noId, ValidRules());

        var noName = ValidMeta();
        noName["name"] = "  ";
        AssertInvalid(noName, ValidRules());
    }

    [Fact]
    public void Rejects_non_deterministic_or_missing_determinism()
    {
        var explicitlyFalse = ValidMeta();
        explicitlyFalse["deterministic"] = false;
        AssertInvalid(explicitlyFalse, ValidRules());

        var missing = ValidMeta();
        missing.Remove("deterministic");
        AssertInvalid(missing, ValidRules());
    }

    [Fact]
    public void Rejects_a_missing_version_object()
    {
        var meta = ValidMeta();
        meta["version"] = null;
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Rejects_a_non_positive_or_missing_version_number()
    {
        var zero = ValidMeta();
        D(zero["version"])["number"] = 0;
        AssertInvalid(zero, ValidRules());

        var missing = ValidMeta();
        D(missing["version"]).Remove("number");
        AssertInvalid(missing, ValidRules());
    }

    [Fact]
    public void Rejects_an_invalid_or_missing_version_status()
    {
        var invalid = ValidMeta();
        D(invalid["version"])["status"] = "archived";
        AssertInvalid(invalid, ValidRules());

        var missing = ValidMeta();
        D(missing["version"]).Remove("status");
        AssertInvalid(missing, ValidRules());
    }

    [Fact]
    public void Rejects_a_blank_rules_hash()
    {
        var meta = ValidMeta();
        D(meta["version"])["rules-hash"] = "";
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Accepts_active_and_retired_statuses()
    {
        var retired = ValidMeta();
        D(retired["version"])["status"] = "retired";
        var def = Load(retired, ValidRules());
        Assert.False(def.IsRunnable); // retired is not runnable
        Assert.Equal(StrategyStatus.Retired, def.Manifest.Version.Status);
    }

    [Fact]
    public void Rejects_a_missing_applicability_object()
    {
        var meta = ValidMeta();
        meta["applicability"] = null;
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Rejects_an_invalid_timeframe()
    {
        var meta = ValidMeta();
        D(meta["applicability"])["timeframe"] = "M3";
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Rejects_a_negative_or_missing_warmup()
    {
        var negative = ValidMeta();
        D(negative["applicability"])["warmup-bars"] = -1;
        AssertInvalid(negative, ValidRules());

        var missing = ValidMeta();
        D(missing["applicability"]).Remove("warmup-bars");
        AssertInvalid(missing, ValidRules());
    }

    [Fact]
    public void Defaults_omitted_exchanges_and_symbols_to_empty()
    {
        var meta = ValidMeta();
        D(meta["applicability"]).Remove("exchanges");
        D(meta["applicability"]).Remove("symbols");

        var def = Load(meta, ValidRules());
        Assert.Empty(def.Manifest.Applicability.Exchanges);
        Assert.Empty(def.Manifest.Applicability.Symbols);
    }

    // ---- parameter validation -------------------------------------------

    [Fact]
    public void Rejects_a_null_parameter_spec()
    {
        var meta = ValidMeta();
        D(meta["parameters"])["rsi-period"] = null;
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Rejects_an_invalid_or_missing_parameter_type()
    {
        var invalid = ValidMeta();
        D(D(invalid["parameters"])["rsi-period"])["type"] = "string";
        AssertInvalid(invalid, ValidRules());

        var missing = ValidMeta();
        D(D(missing["parameters"])["rsi-period"]).Remove("type");
        AssertInvalid(missing, ValidRules());
    }

    [Fact]
    public void Rejects_a_parameter_missing_any_bound()
    {
        foreach (var bound in new[] { "default", "min", "max" })
        {
            var meta = ValidMeta();
            D(D(meta["parameters"])["rsi-period"]).Remove(bound);
            AssertInvalid(meta, ValidRules());
        }
    }

    [Fact]
    public void Rejects_a_parameter_with_min_above_max()
    {
        var meta = ValidMeta();
        var spec = D(D(meta["parameters"])["rsi-period"]);
        spec["min"] = 100;
        spec["max"] = 10;
        AssertInvalid(meta, ValidRules());
    }

    [Fact]
    public void Rejects_a_default_outside_its_bounds_on_either_side()
    {
        var tooHigh = ValidMeta();
        D(D(tooHigh["parameters"])["rsi-period"])["default"] = 999;
        AssertInvalid(tooHigh, ValidRules());

        var tooLow = ValidMeta();
        D(D(tooLow["parameters"])["rsi-period"])["default"] = 1; // min is 2
        AssertInvalid(tooLow, ValidRules());
    }

    [Fact]
    public void Rejects_a_non_integral_int_parameter()
    {
        var meta = ValidMeta();
        D(D(meta["parameters"])["rsi-period"])["default"] = 14.5;
        AssertInvalid(meta, ValidRules());
    }

    // ---- rules identity --------------------------------------------------

    [Fact]
    public void Rejects_rules_with_a_wrong_schema_version()
    {
        var rules = ValidRules();
        rules["schema-version"] = "0.9";
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_strategy_id_mismatch()
    {
        var rules = ValidRules();
        rules["strategy-id"] = "other";
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_version_number_mismatch()
    {
        var rules = ValidRules();
        rules["version-number"] = 2;
        AssertInvalid(ValidMeta(), rules);
    }

    // ---- indicator validation -------------------------------------------

    [Fact]
    public void Rejects_a_null_indicator_entry()
    {
        var rules = ValidRules();
        rules["indicators"] = new object?[] { null };
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_blank_indicator_id()
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["id"] = "";
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_duplicate_indicator_id()
    {
        var rules = ValidRules();
        rules["indicators"] = new object[]
        {
            Indicator("rsi", "rsi", "close", 14),
            Indicator("rsi", "ema", "close", 20),
        };
        AssertInvalid(ValidMeta(), rules);
    }

    [Theory]
    [InlineData("macd")]
    [InlineData("")]
    public void Rejects_an_unknown_or_blank_indicator_kind(string kind)
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["kind"] = kind;
        AssertInvalid(ValidMeta(), rules);
    }

    [Theory]
    [InlineData("vwap")]
    [InlineData("")]
    public void Rejects_an_unknown_or_blank_indicator_source(string source)
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["source"] = source;
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_non_integral_period()
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["period"] = 14.5;
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_period_below_one()
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["period"] = 0;
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_non_reference_string_period()
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["period"] = "fourteen";
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_period_referencing_an_undeclared_parameter()
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["period"] = "$missing";
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_period_of_the_wrong_json_kind()
    {
        var rules = ValidRules();
        D(((object[])rules["indicators"]!)[0])["period"] = true;
        AssertInvalid(ValidMeta(), rules);
    }

    // ---- rule / condition validation ------------------------------------

    [Fact]
    public void Rejects_missing_rules()
    {
        var rules = ValidRules();
        rules["rules"] = null;
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_an_unknown_rule_key()
    {
        var rules = ValidRules();
        rules["rules"] = new Dictionary<string, object?> { ["enter"] = Cmp("gt", "close", 1) };
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_condition_that_is_not_an_object()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = 5;
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_condition_without_exactly_one_operator()
    {
        var none = ValidRules();
        D(none["rules"])["entry-long"] = new Dictionary<string, object?>();
        AssertInvalid(ValidMeta(), none);

        var two = ValidRules();
        D(two["rules"])["entry-long"] = new Dictionary<string, object?>
        {
            ["all"] = Array.Empty<object>(),
            ["any"] = Array.Empty<object>(),
        };
        AssertInvalid(ValidMeta(), two);
    }

    [Fact]
    public void Rejects_a_combinator_whose_value_is_not_an_array()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = new Dictionary<string, object?> { ["all"] = 5 };
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_an_unknown_condition_operator()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = new Dictionary<string, object?> { ["between"] = new object[] { 1, 2 } };
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_comparison_without_two_operands()
    {
        var notArray = ValidRules();
        D(notArray["rules"])["entry-long"] = new Dictionary<string, object?> { ["gt"] = 5 };
        AssertInvalid(ValidMeta(), notArray);

        var wrongLength = ValidRules();
        D(wrongLength["rules"])["entry-long"] = new Dictionary<string, object?> { ["gt"] = new object[] { 1 } };
        AssertInvalid(ValidMeta(), wrongLength);
    }

    // ---- operand validation ---------------------------------------------

    [Fact]
    public void Rejects_an_operand_referencing_an_undeclared_parameter()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = Cmp("gt", "$missing", 1);
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_an_operand_referencing_an_unknown_series()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = Cmp("gt", "vwap", 1);
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_series_object_without_a_string_series()
    {
        var missing = ValidRules();
        D(missing["rules"])["entry-long"] = Cmp("gt", new Dictionary<string, object?> { ["offset"] = 1 }, 1);
        AssertInvalid(ValidMeta(), missing);

        var notString = ValidRules();
        D(notString["rules"])["entry-long"] = Cmp("gt", new Dictionary<string, object?> { ["series"] = 5 }, 1);
        AssertInvalid(ValidMeta(), notString);
    }

    [Fact]
    public void Rejects_a_non_integer_offset()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = Cmp(
            "gt",
            new Dictionary<string, object?> { ["series"] = "close", ["offset"] = "x" },
            1
        );
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_a_negative_offset()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = Cmp(
            "gt",
            new Dictionary<string, object?> { ["series"] = "close", ["offset"] = -1 },
            1
        );
        AssertInvalid(ValidMeta(), rules);
    }

    [Fact]
    public void Rejects_an_operand_of_an_unsupported_kind()
    {
        var rules = ValidRules();
        D(rules["rules"])["entry-long"] = Cmp("gt", true, 1);
        AssertInvalid(ValidMeta(), rules);
    }

    // ---- advisory blocks -------------------------------------------------

    [Fact]
    public void Requested_risk_defaults_omitted_fields_to_zero()
    {
        var rules = ValidRules();
        rules["requested-risk"] = new Dictionary<string, object?> { ["stop-loss-pct"] = 1.5 };

        var def = Load(ValidMeta(), rules);
        Assert.Equal(1.5m, def.Rules.RequestedRisk!.StopLossPct);
        Assert.Equal(0m, def.Rules.RequestedRisk.TakeProfitPct);
        Assert.Equal(0m, def.Rules.RequestedRisk.MaxPositionPct);
    }

    [Fact]
    public void Execution_defaults_omitted_fields_and_parses_each_enum()
    {
        var rules = ValidRules();
        rules["execution"] = new Dictionary<string, object?> { ["order-type"] = "limit", ["time-in-force"] = "ioc" };
        var def = Load(ValidMeta(), rules);
        Assert.Equal(OrderType.Limit, def.Rules.Execution!.OrderType);
        Assert.Equal(TimeInForce.Ioc, def.Rules.Execution.TimeInForce);
        Assert.Equal(0m, def.Rules.Execution.SlippageTolerancePct);

        var fok = ValidRules();
        fok["execution"] = new Dictionary<string, object?> { ["time-in-force"] = "fok" };
        var fokDef = Load(ValidMeta(), fok);
        Assert.Equal(OrderType.Market, fokDef.Rules.Execution!.OrderType); // defaulted
        Assert.Equal(TimeInForce.Fok, fokDef.Rules.Execution.TimeInForce);

        var orderOnly = ValidRules();
        orderOnly["execution"] = new Dictionary<string, object?> { ["order-type"] = "market" };
        var orderOnlyDef = Load(ValidMeta(), orderOnly);
        Assert.Equal(TimeInForce.Gtc, orderOnlyDef.Rules.Execution!.TimeInForce); // defaulted
    }

    [Fact]
    public void Rejects_an_invalid_order_type_or_time_in_force()
    {
        var badOrder = ValidRules();
        D(badOrder["execution"])["order-type"] = "stop";
        AssertInvalid(ValidMeta(), badOrder);

        var badTif = ValidRules();
        D(badTif["execution"])["time-in-force"] = "day";
        AssertInvalid(ValidMeta(), badTif);
    }
}
