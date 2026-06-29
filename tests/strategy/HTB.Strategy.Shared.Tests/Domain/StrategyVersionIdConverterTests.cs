using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class StrategyVersionIdConverterTests
{
    private static readonly StrategyVersionIdConverter Converter = new();

    [Fact]
    public void Converts_a_version_id_to_its_string_form()
    {
        var versionId = new StrategyVersionId(new StrategyId("rsi-movement"), new StrategyVersion(2));

        var stored = Converter.ConvertToProvider(versionId);

        Assert.Equal("rsi-movement@2", stored);
    }

    [Fact]
    public void Converts_a_stored_string_back_to_a_version_id()
    {
        var restored = Converter.ConvertFromProvider("rsi-movement@2");

        Assert.Equal(new StrategyVersionId(new StrategyId("rsi-movement"), new StrategyVersion(2)), restored);
    }
}
