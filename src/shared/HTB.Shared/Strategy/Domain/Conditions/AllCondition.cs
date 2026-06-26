namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>
/// Boolean AND of its children (the DSL <c>{ "all": [ … ] }</c>). Vacuously true when empty and
/// short-circuits on the first unsatisfied child.
/// </summary>
public sealed class AllCondition(IReadOnlyList<ICondition> conditions) : ICondition
{
    public IReadOnlyList<ICondition> Conditions { get; } =
        conditions ?? throw new ArgumentNullException(nameof(conditions));

    public bool IsSatisfiedBy(EvaluationContext context)
    {
        foreach (var condition in Conditions)
        {
            if (!condition.IsSatisfiedBy(context))
            {
                return false;
            }
        }

        return true;
    }
}
