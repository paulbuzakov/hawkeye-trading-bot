namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// Comparison applied between the two operands of a <see cref="Condition"/>. The cross operators
/// compare the current bar against the previous one (an edge), unlike the instantaneous relations.
/// Stored as a stable numeric code; never renumber existing members.
/// </summary>
public enum ComparisonOperator : byte
{
    LessThan = 1,
    LessThanOrEqual = 2,
    GreaterThan = 3,
    GreaterThanOrEqual = 4,
    Equal = 5,
    NotEqual = 6,

    /// <summary>Left crosses from below to above right between the previous bar and this one.</summary>
    CrossesAbove = 7,

    /// <summary>Left crosses from above to below right between the previous bar and this one.</summary>
    CrossesBelow = 8,
}
