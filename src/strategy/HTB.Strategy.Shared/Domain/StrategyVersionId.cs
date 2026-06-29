namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// Fully-qualified identity of one immutable strategy version — a <see cref="StrategyId"/>
/// paired with its <see cref="StrategyVersion"/>. This is the key downstream components
/// (signals, backtest runs, live deployments) reference, since a strategy's behaviour is
/// only well-defined for a specific version. Renders as <c>{id}@{version}</c>, e.g.
/// <c>rsi-movement@1</c>.
/// </summary>
public readonly record struct StrategyVersionId
{
    public const char Separator = '@';

    public StrategyVersionId(StrategyId id, StrategyVersion version)
    {
        Id = id;
        Version = version;
    }

    public StrategyId Id { get; }

    public StrategyVersion Version { get; }

    public override string ToString() => $"{Id}{Separator}{Version}";

    public static StrategyVersionId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StrategyDomainException("Strategy version id must be a non-empty '{id}@{version}' string.");
        }

        var parts = value.Split(Separator);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var version))
        {
            throw new StrategyDomainException(
                $"Strategy version id '{value}' is not a valid '{{id}}@{{version}}' string."
            );
        }

        return new StrategyVersionId(new StrategyId(parts[0]), new StrategyVersion(version));
    }
}
