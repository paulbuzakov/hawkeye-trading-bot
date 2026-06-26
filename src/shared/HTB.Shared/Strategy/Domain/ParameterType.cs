namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The value domain of a declared strategy parameter. Schema <c>1.0</c> supports only numeric
/// parameters; booleans/enums-as-parameters are deliberately out of scope (see the format doc §9.5).
/// </summary>
public enum ParameterType : short
{
    /// <summary>A whole number (e.g. an indicator period). <c>default</c>/<c>min</c>/<c>max</c> must be integral.</summary>
    Int = 1,

    /// <summary>A real number (e.g. an RSI threshold).</summary>
    Decimal = 2,
}
