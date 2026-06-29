namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// One side of a <see cref="Condition"/> — a tagged union over the four things a rule can
/// compare: a numeric <see cref="OperandKind.Literal"/>, a <see cref="OperandKind.Parameter"/>
/// reference (<c>$name</c>), an <see cref="OperandKind.Indicator"/> reference, or a
/// <see cref="OperandKind.PriceField"/> of the current bar. The relevant payload property is
/// populated for the active <see cref="Kind"/>; the others are <c>null</c>. Construct via the
/// static factories — the private constructor keeps the union closed and consistent.
/// </summary>
public sealed record Operand
{
    private Operand(OperandKind kind, decimal? number, string? name, PriceSource? field)
    {
        Kind = kind;
        Number = number;
        Name = name;
        Field = field;
    }

    /// <summary>Which kind of operand this is; selects the populated payload.</summary>
    public OperandKind Kind { get; }

    /// <summary>The constant value when <see cref="Kind"/> is <see cref="OperandKind.Literal"/>.</summary>
    public decimal? Number { get; }

    /// <summary>
    /// The referenced parameter or indicator name when <see cref="Kind"/> is
    /// <see cref="OperandKind.Parameter"/> or <see cref="OperandKind.Indicator"/>.
    /// </summary>
    public string? Name { get; }

    /// <summary>The bar field when <see cref="Kind"/> is <see cref="OperandKind.PriceField"/>.</summary>
    public PriceSource? Field { get; }

    /// <summary>A constant numeric operand.</summary>
    public static Operand Literal(decimal value) => new(OperandKind.Literal, value, null, null);

    /// <summary>A reference to a declared parameter by name (the <c>$name</c> form).</summary>
    public static Operand Parameter(string name) =>
        new(OperandKind.Parameter, null, RequireName(name, "parameter"), null);

    /// <summary>A reference to a declared indicator by name.</summary>
    public static Operand Indicator(string name) =>
        new(OperandKind.Indicator, null, RequireName(name, "indicator"), null);

    /// <summary>A field of the current price bar.</summary>
    public static Operand Price(PriceSource field) => new(OperandKind.PriceField, null, null, field);

    private static string RequireName(string name, string kind)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StrategyDomainException($"Operand {kind} reference must name a non-empty target.");
        }

        return name.Trim();
    }
}
