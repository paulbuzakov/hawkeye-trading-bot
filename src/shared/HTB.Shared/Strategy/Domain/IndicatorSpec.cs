namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// A derived series the rules reference by <see cref="Id"/>. <see cref="Kind"/> comes from the
/// closed registry of indicator implementations; <see cref="Period"/> is already resolved to a
/// concrete value (a literal, or a parameter's bound value) at load time.
/// </summary>
public sealed record IndicatorSpec(string Id, string Kind, CandleField Source, int Period);
