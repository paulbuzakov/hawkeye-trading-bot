using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Tests.Domain;

public sealed class ParameterSpecTests
{
    [Fact]
    public void Constructor_keeps_envelope_values()
    {
        var spec = new ParameterSpec("oversold", @default: 30m, min: 20m, max: 40m, step: 5m);

        Assert.Equal("oversold", spec.Name);
        Assert.Equal(30m, spec.Default);
        Assert.Equal(20m, spec.Min);
        Assert.Equal(40m, spec.Max);
        Assert.Equal(5m, spec.Step);
    }

    [Fact]
    public void Constructor_trims_the_name()
    {
        var spec = new ParameterSpec("  rsiPeriod  ", @default: 14m, min: 7m, max: 21m, step: 1m);

        Assert.Equal("rsiPeriod", spec.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_name(string name)
    {
        Assert.Throws<StrategyDomainException>(
            () => new ParameterSpec(name, @default: 1m, min: 0m, max: 2m, step: 1m)
        );
    }

    [Fact]
    public void Constructor_rejects_min_above_max()
    {
        Assert.Throws<StrategyDomainException>(
            () => new ParameterSpec("p", @default: 5m, min: 10m, max: 4m, step: 1m)
        );
    }

    [Fact]
    public void Constructor_rejects_default_below_min()
    {
        Assert.Throws<StrategyDomainException>(
            () => new ParameterSpec("p", @default: 1m, min: 5m, max: 10m, step: 1m)
        );
    }

    [Fact]
    public void Constructor_rejects_default_above_max()
    {
        Assert.Throws<StrategyDomainException>(
            () => new ParameterSpec("p", @default: 11m, min: 5m, max: 10m, step: 1m)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_step(decimal step)
    {
        Assert.Throws<StrategyDomainException>(
            () => new ParameterSpec("p", @default: 5m, min: 0m, max: 10m, step: step)
        );
    }
}
