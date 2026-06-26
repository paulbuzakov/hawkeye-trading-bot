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

    /// <summary>
    /// Non-throwing check step: returns whether the pair would load (and the loaded definition when it
    /// does), instead of throwing on the first validation failure. For governance/registry admission.
    /// A null argument is still a programming error and throws <see cref="ArgumentNullException"/>.
    /// </summary>
    StrategyValidationResult Validate(string manifestJson, string rulesJson);
}
