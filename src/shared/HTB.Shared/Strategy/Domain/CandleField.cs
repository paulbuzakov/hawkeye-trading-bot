using HTB.Shared.MarketData.Domain;

namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// A candle field an indicator may consume or an operand may read. The DSL names them in
/// lower-case (<c>close</c>, <c>open</c>, …) which parse case-insensitively to these members.
/// </summary>
public enum CandleField : short
{
    Open = 1,
    High = 2,
    Low = 3,
    Close = 4,
    Volume = 5,
}

/// <summary>Helpers to parse a DSL field name and read it off a <see cref="Candle"/>.</summary>
public static class CandleFields
{
    /// <summary>Parses a DSL field name (case-insensitive) to a defined <see cref="CandleField"/>.</summary>
    public static bool TryParse(string name, out CandleField field) =>
        Enum.TryParse(name, ignoreCase: true, out field) && Enum.IsDefined(field);

    /// <summary>Reads the value of <paramref name="field"/> off <paramref name="candle"/>.</summary>
    public static decimal ValueOf(CandleField field, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);
        return field switch
        {
            CandleField.Open => candle.Open,
            CandleField.High => candle.High,
            CandleField.Low => candle.Low,
            CandleField.Close => candle.Close,
            CandleField.Volume => candle.Volume,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown candle field."),
        };
    }
}
