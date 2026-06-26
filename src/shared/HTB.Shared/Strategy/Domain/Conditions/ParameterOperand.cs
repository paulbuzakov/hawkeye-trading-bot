namespace HTB.Shared.Strategy.Domain.Conditions;

/// <summary>
/// A reference to a bound strategy parameter (the DSL form <c>"$rsi-oversold"</c>, stored here as
/// <c>rsi-oversold</c> without the leading <c>$</c>).
/// </summary>
public sealed class ParameterOperand(string name) : IOperand
{
    public string Name { get; } = !string.IsNullOrEmpty(name)
        ? name
        : throw new ArgumentException("Parameter name must be non-empty.", nameof(name));

    public decimal Evaluate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetParameter(Name);
    }
}
