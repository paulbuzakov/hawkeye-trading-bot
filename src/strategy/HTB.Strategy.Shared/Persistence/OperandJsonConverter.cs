using System.Text.Json;
using System.Text.Json.Serialization;
using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Shared.Persistence;

/// <summary>
/// System.Text.Json converter for the <see cref="Operand"/> tagged union. The union has a private
/// constructor and factory methods, so the default reflection-based serializer cannot round-trip it;
/// this converter writes the <see cref="OperandKind"/> discriminator plus only the populated payload
/// and reconstructs through the matching factory (re-running its validation on read).
/// </summary>
public sealed class OperandJsonConverter : JsonConverter<Operand>
{
    private const string KindProperty = "kind";
    private const string NumberProperty = "number";
    private const string NameProperty = "name";
    private const string FieldProperty = "field";

    public override Operand Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = (OperandKind)root.GetProperty(KindProperty).GetByte();

        return kind switch
        {
            OperandKind.Literal => Operand.Literal(root.GetProperty(NumberProperty).GetDecimal()),
            OperandKind.Parameter => Operand.Parameter(root.GetProperty(NameProperty).GetString()!),
            OperandKind.Indicator => Operand.Indicator(root.GetProperty(NameProperty).GetString()!),
            OperandKind.PriceField => Operand.Price((PriceSource)root.GetProperty(FieldProperty).GetByte()),
            _ => throw new StrategyDomainException($"Unknown operand kind '{(byte)kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, Operand value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(KindProperty, (byte)value.Kind);

        if (value.Number is { } number)
        {
            writer.WriteNumber(NumberProperty, number);
        }

        if (value.Name is { } name)
        {
            writer.WriteString(NameProperty, name);
        }

        if (value.Field is { } field)
        {
            writer.WriteNumber(FieldProperty, (byte)field);
        }

        writer.WriteEndObject();
    }
}
