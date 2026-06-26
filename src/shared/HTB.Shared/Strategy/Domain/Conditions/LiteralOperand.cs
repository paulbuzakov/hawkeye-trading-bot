namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>A constant numeric operand (e.g. the literal <c>30</c>).</summary>
public sealed class LiteralOperand(decimal value) : IOperand
{
    public decimal Value { get; } = value;

    public decimal Evaluate(EvaluationContext context) => Value;
}
