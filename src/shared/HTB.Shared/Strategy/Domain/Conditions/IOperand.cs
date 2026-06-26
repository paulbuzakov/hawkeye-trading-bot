namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>
/// A leaf of the condition DSL that resolves to a <see cref="decimal"/> against the decision bar
/// (the Interpreter pattern's terminal expression). Implementations are pure and side-effect-free.
/// </summary>
public interface IOperand
{
    decimal Evaluate(EvaluationContext context);
}
