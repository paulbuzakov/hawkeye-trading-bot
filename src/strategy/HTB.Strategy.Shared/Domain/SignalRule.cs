namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// A signal trigger — a set of <see cref="Conditions"/> combined by a <see cref="LogicalOperator"/>.
/// Models a <c>rules.json</c> <c>entry</c> (<c>all</c>) or <c>exit</c> (<c>any</c>) block. At least
/// one condition is required; an empty rule would fire on every (or no) bar, which is never intended.
/// </summary>
public sealed record SignalRule
{
    public SignalRule(LogicalOperator mode, IReadOnlyList<Condition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        if (conditions.Count == 0)
        {
            throw new StrategyDomainException("A signal rule must declare at least one condition.");
        }

        Mode = mode;
        Conditions = conditions;
    }

    /// <summary>Whether all (AND) or any (OR) of the conditions must hold to fire.</summary>
    public LogicalOperator Mode { get; }

    /// <summary>The conditions evaluated against a bar; never empty.</summary>
    public IReadOnlyList<Condition> Conditions { get; }
}
