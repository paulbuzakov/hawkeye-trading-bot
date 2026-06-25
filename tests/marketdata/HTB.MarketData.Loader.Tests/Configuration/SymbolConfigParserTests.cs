using HTB.MarketData.Loader.Configuration;
using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Tests.Configuration;

public class SymbolConfigParserTests
{
    [Fact]
    public void Parse_reads_all_entries_with_timeframes_and_dates()
    {
        const string json = """
            [
              {
                "ticket": "BTCUSDT",
                "timeframes": ["M1", "M5"],
                "date-range": { "from": "2020-01-01", "to": "2026-12-31" }
              },
              {
                "ticket": "ETHUSDT",
                "timeframes": ["H1"],
                "date-range": { "from": "2021-06-15" }
              }
            ]
            """;

        var specs = SymbolConfigParser.Parse(json);

        Assert.Equal(2, specs.Count);

        Assert.Equal("BTCUSDT", specs[0].Ticker);
        Assert.Equal([Timeframe.M1, Timeframe.M5], specs[0].Timeframes);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), specs[0].From);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), specs[0].To);

        Assert.Equal("ETHUSDT", specs[1].Ticker);
        Assert.Equal([Timeframe.H1], specs[1].Timeframes);
        Assert.Null(specs[1].To);
    }

    [Fact]
    public void Parse_is_case_insensitive_for_timeframes()
    {
        const string json = """
            [{ "ticket": "BTCUSDT", "timeframes": ["m1"], "date-range": { "from": "2020-01-01" } }]
            """;

        var specs = SymbolConfigParser.Parse(json);

        Assert.Equal([Timeframe.M1], specs[0].Timeframes);
    }

    [Fact]
    public void Parse_tolerates_comments_and_trailing_commas()
    {
        const string json = """
            [
              // first pair
              { "ticket": "BTCUSDT", "timeframes": ["M1"], "date-range": { "from": "2020-01-01" }, },
            ]
            """;

        var specs = SymbolConfigParser.Parse(json);

        Assert.Single(specs);
    }

    [Fact]
    public void Parse_null_argument_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SymbolConfigParser.Parse(null!));
    }

    [Fact]
    public void Parse_invalid_json_throws_config_exception()
    {
        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse("{ not json"));
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public void Parse_json_null_literal_throws()
    {
        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse("null"));
        Assert.Contains("array of entries", ex.Message);
    }

    [Fact]
    public void Parse_null_entry_throws()
    {
        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse("[null]"));
        Assert.Contains("#0 is null", ex.Message);
    }

    [Theory]
    [InlineData("""[{ "timeframes": ["M1"], "date-range": { "from": "2020-01-01" } }]""")]
    [InlineData(
        """[{ "ticket": "  ", "timeframes": ["M1"], "date-range": { "from": "2020-01-01" } }]"""
    )]
    public void Parse_missing_ticket_throws(string json)
    {
        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("ticket", ex.Message);
    }

    [Theory]
    [InlineData("""[{ "ticket": "BTCUSDT", "date-range": { "from": "2020-01-01" } }]""")]
    [InlineData(
        """[{ "ticket": "BTCUSDT", "timeframes": [], "date-range": { "from": "2020-01-01" } }]"""
    )]
    public void Parse_missing_timeframes_throws(string json)
    {
        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("timeframe", ex.Message);
    }

    [Fact]
    public void Parse_unknown_timeframe_throws()
    {
        const string json = """
            [{ "ticket": "BTCUSDT", "timeframes": ["W1"], "date-range": { "from": "2020-01-01" } }]
            """;

        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("unknown timeframe", ex.Message);
    }

    [Fact]
    public void Parse_missing_date_range_throws()
    {
        const string json = """[{ "ticket": "BTCUSDT", "timeframes": ["M1"] }]""";

        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("date-range", ex.Message);
    }

    [Theory]
    [InlineData("""[{ "ticket": "BTCUSDT", "timeframes": ["M1"], "date-range": { } }]""")]
    [InlineData(
        """[{ "ticket": "BTCUSDT", "timeframes": ["M1"], "date-range": { "from": "  " } }]"""
    )]
    public void Parse_missing_from_date_throws(string json)
    {
        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("from", ex.Message);
    }

    [Fact]
    public void Parse_invalid_from_date_throws()
    {
        const string json = """
            [{ "ticket": "BTCUSDT", "timeframes": ["M1"], "date-range": { "from": "not-a-date" } }]
            """;

        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("invalid", ex.Message);
    }

    [Fact]
    public void Parse_invalid_to_date_throws()
    {
        const string json = """
            [{ "ticket": "BTCUSDT", "timeframes": ["M1"],
               "date-range": { "from": "2020-01-01", "to": "later" } }]
            """;

        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("invalid", ex.Message);
    }

    [Fact]
    public void Parse_to_before_from_throws()
    {
        const string json = """
            [{ "ticket": "BTCUSDT", "timeframes": ["M1"],
               "date-range": { "from": "2026-01-01", "to": "2020-01-01" } }]
            """;

        var ex = Assert.Throws<SymbolConfigException>(() => SymbolConfigParser.Parse(json));
        Assert.Contains("before its", ex.Message);
    }

    [Fact]
    public async Task ParseFileAsync_reads_manifest_from_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"symbols-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            path,
            """[{ "ticket": "BTCUSDT", "timeframes": ["M1"], "date-range": { "from": "2020-01-01" } }]"""
        );

        try
        {
            var specs = await SymbolConfigParser.ParseFileAsync(path);
            Assert.Equal("BTCUSDT", Assert.Single(specs).Ticker);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseFileAsync_blank_path_throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => SymbolConfigParser.ParseFileAsync("  "));
    }
}
