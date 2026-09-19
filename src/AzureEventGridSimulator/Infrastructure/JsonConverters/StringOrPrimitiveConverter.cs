using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.JsonConverters;

/// <summary>
///     JSON converter that accepts strings, numbers, and booleans and converts them to strings.
///     Azure Event Grid is lenient and coerces primitive types to strings.
/// </summary>
/// <remarks>
///     Numbers that don't parse as an Int64 are normalised through <see cref="double" />, so their
///     original text isn't kept (for example <c>1.0</c> becomes "1" and <c>1e3</c> becomes "1000").
/// </remarks>
public sealed class StringOrPrimitiveConverter : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => throw new JsonException(
                $"Unexpected token type {reader.TokenType}. Expected string, number, or boolean."
            ),
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
