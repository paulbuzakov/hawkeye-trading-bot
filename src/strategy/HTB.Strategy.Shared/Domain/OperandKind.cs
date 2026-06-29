namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// Discriminator for an <see cref="Operand"/> — what one side of a <see cref="Condition"/>
/// resolves to. Stored as a stable numeric code; never renumber existing members.
/// </summary>
public enum OperandKind : byte
{
    /// <summary>A constant numeric value.</summary>
    Literal = 1,

    /// <summary>A reference to a declared <see cref="ParameterSpec"/> by name (the <c>$name</c> form).</summary>
    Parameter = 2,

    /// <summary>A reference to a declared <see cref="IndicatorSpec"/> by name.</summary>
    Indicator = 3,

    /// <summary>A field of the current bar (see <see cref="PriceSource"/>).</summary>
    PriceField = 4,
}
