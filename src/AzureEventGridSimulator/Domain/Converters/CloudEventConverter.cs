namespace AzureEventGridSimulator.Domain.Converters;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;

internal class CloudEventConverter : JsonConverter<CloudEvent>
{
    public static readonly string MaximumAllowedEventGridEventSizeErrorMesage = $"The maximum size for the JSON content({MaximumAllowedEventGridEventSizeInBytes}) has been exceeded.";

    private const int MaximumAllowedEventGridEventSizeInBytes = 1049600;
    private static readonly IReadOnlyDictionary<string, Action<CloudEvent, JsonElement>> _propertyMap;

    static CloudEventConverter()
    {
        _propertyMap = new Dictionary<string, Action<CloudEvent, JsonElement>>
        {
            [CloudEventConstants.SpecVersion] = (ege, elem) => ege.SpecVersion = elem.GetString(),
            [CloudEventConstants.Id] = (ege, elem) => ege.Id = elem.GetString(),
            [CloudEventConstants.Source] = (ege, elem) => ege.Source = elem.GetString(),
            [CloudEventConstants.Type] = (ege, elem) => ege.Type = elem.GetString(),
            [CloudEventConstants.DataContentType] = (ege, elem) => ege.DataContentType = elem.ToString(),
            [CloudEventConstants.DataSchema] = (ege, elem) => ege.DataSchema = elem.ToString(),
            [CloudEventConstants.Subject] = (ege, elem) => ege.Subject = elem.GetString(),
            [CloudEventConstants.Time] = (ege, elem) => ege.Time = elem.GetDateTimeOffset("O").ToUniversalTime().ToString(),
            [CloudEventConstants.Data] = (ege, elem) => ege.Data = elem.Clone(),
            [CloudEventConstants.DataBase64] = (ege, elem) => ege.DataBase64 = elem.GetString()
        };
    }

    public override CloudEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(typeToConvert == typeof(CloudEvent));

        var start = reader.TokenStartIndex;

        var requestDocument = JsonDocument.ParseValue(ref reader);
        var target = new CloudEvent();
        foreach (var property in requestDocument.RootElement.EnumerateObject())
        {
            if (_propertyMap.TryGetValue(property.Name, out var value))
            {
                value(target, property.Value);
            }
            else
            {
                target.ExtensionAttributes[property.Name] = GetObject(property.Value);
            }
        }

        var end = reader.TokenStartIndex;
        var length = end - start;

        if (length > MaximumAllowedEventGridEventSizeInBytes)
        {
            throw new JsonException(MaximumAllowedEventGridEventSizeErrorMesage);
        }

        target.Validate();

        return target;
    }

    public override void Write(Utf8JsonWriter writer, CloudEvent value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    private static object GetObject(in JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out int intValue))
                {
                    return intValue;
                }
                if (element.TryGetInt64(out long longValue))
                {
                    return longValue;
                }
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Object:
                var dictionary = new Dictionary<string, object?>();
                foreach (JsonProperty jsonProperty in element.EnumerateObject())
                {
                    dictionary.Add(jsonProperty.Name, GetObject(jsonProperty.Value));
                }
                return dictionary;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    list.Add(GetObject(item));
                }
                return list.ToArray();
            default:
                throw new NotSupportedException("Not supported value kind " + element.ValueKind);
        }
    }
}
