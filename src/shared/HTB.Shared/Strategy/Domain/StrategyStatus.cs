namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// Lifecycle state of a strategy <see cref="StrategyVersion"/>. Stored as a <see cref="short"/>
/// code so the on-disk value is stable and independent of declaration order.
/// </summary>
public enum StrategyStatus : short
{
    /// <summary>Editable; its <c>rules-hash</c> is not yet verified and it must not be run.</summary>
    Draft = 1,

    /// <summary>Immutable and runnable; its <c>rules-hash</c> is verified against <c>rules.json</c>.</summary>
    Active = 2,

    /// <summary>No new runs may start; kept only for audit of historical runs.</summary>
    Retired = 3,
}
