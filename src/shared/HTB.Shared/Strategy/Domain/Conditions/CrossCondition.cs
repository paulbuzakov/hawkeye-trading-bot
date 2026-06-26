namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>The direction of a <see cref="CrossCondition"/>.</summary>
public enum CrossDirection : short
{
    /// <summary><c>crosses-above</c>: left ≤ right one bar ago, and left &gt; right now.</summary>
    Above = 1,

    /// <summary><c>crosses-below</c>: left ≥ right one bar ago, and left &lt; right now.</summary>
    Below = 2,
}

/// <summary>
/// True when <see cref="Left"/> crosses <see cref="Right"/> between the prior bar and the decision
/// bar. This encapsulates the only sanctioned look-back (offset 1) so authors never hand-roll it
/// and cannot accidentally peek forward.
/// </summary>
public sealed class CrossCondition(CrossDirection direction, IOperand left, IOperand right)
    : ICondition
{
    public CrossDirection Direction { get; } = direction;

    public IOperand Left { get; } = left ?? throw new ArgumentNullException(nameof(left));

    public IOperand Right { get; } = right ?? throw new ArgumentNullException(nameof(right));

    public bool IsSatisfiedBy(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = context.ShiftBack(1);

        var leftNow = Left.Evaluate(context);
        var rightNow = Right.Evaluate(context);
        var leftPrev = Left.Evaluate(previous);
        var rightPrev = Right.Evaluate(previous);

        return Direction switch
        {
            CrossDirection.Above => leftPrev <= rightPrev && leftNow > rightNow,
            CrossDirection.Below => leftPrev >= rightPrev && leftNow < rightNow,
            _ => throw new ArgumentOutOfRangeException(
                nameof(context),
                Direction,
                "Unknown cross direction."
            ),
        };
    }
}
