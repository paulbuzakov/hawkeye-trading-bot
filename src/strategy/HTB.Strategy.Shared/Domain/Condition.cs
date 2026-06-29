namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// A single boolean test — the <c>{ "left": …, "op": …, "right": … }</c> shape in a
/// <c>rules.json</c> <c>entry</c>/<c>exit</c> block. Evaluates <see cref="Left"/>
/// <see cref="Operator"/> <see cref="Right"/> on a bar. Both sides are required.
/// </summary>
public sealed record Condition
{
    public Condition(Operand left, ComparisonOperator @operator, Operand right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        Left = left;
        Operator = @operator;
        Right = right;
    }

    /// <summary>Left-hand operand.</summary>
    public Operand Left { get; }

    /// <summary>Comparison between the operands.</summary>
    public ComparisonOperator Operator { get; }

    /// <summary>Right-hand operand.</summary>
    public Operand Right { get; }
}
