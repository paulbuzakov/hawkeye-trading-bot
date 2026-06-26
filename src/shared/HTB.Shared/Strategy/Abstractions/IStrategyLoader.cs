using HTB.Shared.Strategy.Domain;

namespace HTB.Shared.Strategy.Abstractions;

/// <summary>
/// Turns the untrusted <c>meta.json</c> + <c>rules.json</c> pair into a validated, hashed,
/// immutable <see cref="StrategyDefinition"/> (the one place where JSON becomes a runnable object).
/// Implementations throw a typed configuration exception on any validation failure.
/// </summary>
public interface IStrategyLoader
{
    StrategyDefinition Load(string manifestJson, string rulesJson);
}
