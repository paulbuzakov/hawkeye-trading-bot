namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The bounded specification of one strategy parameter, declared in <c>meta.json</c>. The bounds
/// are a governance contract: they define the legal range a tuner/optimizer may sweep and that
/// risk review signs off on. <c>rules.json</c> only references parameters by name; it never
/// defines their range. Invariant: <see cref="Min"/> ≤ <see cref="Default"/> ≤ <see cref="Max"/>.
/// </summary>
public sealed record ParameterSpec(ParameterType Type, decimal Default, decimal Min, decimal Max);
