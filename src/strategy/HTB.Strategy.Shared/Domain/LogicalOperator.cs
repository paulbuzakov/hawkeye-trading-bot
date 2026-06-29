namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// How a <see cref="SignalRule"/> combines its conditions: <see cref="All"/> is a conjunction
/// (the <c>all</c> block), <see cref="Any"/> is a disjunction (the <c>any</c> block). Stored as a
/// stable numeric code; never renumber existing members.
/// </summary>
public enum LogicalOperator : byte
{
    /// <summary>Every condition must hold (AND).</summary>
    All = 1,

    /// <summary>At least one condition must hold (OR).</summary>
    Any = 2,
}
