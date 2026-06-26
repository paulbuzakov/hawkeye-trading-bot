namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>
/// A node of the compiled condition tree (the Specification pattern over the decision bar).
/// Implementations are pure: the same context always yields the same boolean.
/// </summary>
public interface ICondition
{
    bool IsSatisfiedBy(EvaluationContext context);
}
