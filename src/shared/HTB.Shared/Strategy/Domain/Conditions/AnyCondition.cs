namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>
/// Boolean OR of its children (the DSL <c>{ "any": [ … ] }</c>). Vacuously false when empty and
/// short-circuits on the first satisfied child.
/// </summary>
public sealed class AnyCondition(IReadOnlyList<ICondition> conditions) : ICondition
{
    public IReadOnlyList<ICondition> Conditions { get; } =
        conditions ?? throw new ArgumentNullException(nameof(conditions));

    public bool IsSatisfiedBy(EvaluationContext context)
    {
        foreach (var condition in Conditions)
        {
            if (condition.IsSatisfiedBy(context))
            {
                return true;
            }
        }

        return false;
    }
}
