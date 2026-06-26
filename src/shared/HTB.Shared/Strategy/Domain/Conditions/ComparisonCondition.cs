namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>The relational operators of the DSL (<c>gt</c>, <c>gte</c>, <c>lt</c>, <c>lte</c>, <c>eq</c>).</summary>
public enum ComparisonOperator : short
{
    GreaterThan = 1,
    GreaterThanOrEqual = 2,
    LessThan = 3,
    LessThanOrEqual = 4,
    Equal = 5,
}

/// <summary>
/// Compares two operands with a relational <see cref="ComparisonOperator"/> (the DSL
/// <c>{ "&lt;op&gt;": [ left, right ] }</c>). Both operands read the same decision bar.
/// </summary>
public sealed class ComparisonCondition(ComparisonOperator op, IOperand left, IOperand right)
    : ICondition
{
    public ComparisonOperator Operator { get; } = op;

    public IOperand Left { get; } = left ?? throw new ArgumentNullException(nameof(left));

    public IOperand Right { get; } = right ?? throw new ArgumentNullException(nameof(right));

    public bool IsSatisfiedBy(EvaluationContext context)
    {
        var left = Left.Evaluate(context);
        var right = Right.Evaluate(context);
        return Operator switch
        {
            ComparisonOperator.GreaterThan => left > right,
            ComparisonOperator.GreaterThanOrEqual => left >= right,
            ComparisonOperator.LessThan => left < right,
            ComparisonOperator.LessThanOrEqual => left <= right,
            ComparisonOperator.Equal => left == right,
            _ => throw new ArgumentOutOfRangeException(
                nameof(context),
                Operator,
                "Unknown comparison operator."
            ),
        };
    }
}
