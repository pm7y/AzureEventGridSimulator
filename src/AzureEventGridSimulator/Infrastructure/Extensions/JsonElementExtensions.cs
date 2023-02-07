namespace AzureEventGridSimulator.Infrastructure.Extensions;

using System;
using System.Globalization;
using System.Text.Json;

internal static class JsonElementExtensions
{
    public static DateTimeOffset GetDateTimeOffset(in this JsonElement element, string format)
    {
        return format switch
        {
            "U" when element.ValueKind == JsonValueKind.Number => DateTimeOffset.FromUnixTimeSeconds(element.GetInt64()),
            // relying on the param check of the inner call to throw ArgumentNullException if GetString() returns null
            _ => ParseDateTimeOffset(element.GetString()!, format)
        };
    }

    public static DateTimeOffset ParseDateTimeOffset(string value, string format)
    {
        return format switch
        {
            "U" => DateTimeOffset.FromUnixTimeSeconds(long.Parse(value, CultureInfo.InvariantCulture)),
            _ => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
        };
    }
}
