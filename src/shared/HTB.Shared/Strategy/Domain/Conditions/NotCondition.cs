namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>Boolean negation of its child (the DSL <c>{ "not": node }</c>).</summary>
public sealed class NotCondition(ICondition condition) : ICondition
{
    public ICondition Condition { get; } =
        condition ?? throw new ArgumentNullException(nameof(condition));

    public bool IsSatisfiedBy(EvaluationContext context) => !Condition.IsSatisfiedBy(context);
}
