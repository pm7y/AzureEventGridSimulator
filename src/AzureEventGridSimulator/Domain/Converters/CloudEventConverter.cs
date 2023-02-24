namespace AzureEventGridSimulator.Domain.Converters;

using System;
using System.Collections.Generic;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;

public class CloudEventConverter : EventConverter<CloudEvent>
{
    private static readonly IReadOnlyDictionary<string, Action<CloudEvent, JsonElement>> _propertyMap;

    static CloudEventConverter()
    {
        _propertyMap = new Dictionary<string, Action<CloudEvent, JsonElement>>
        {
            [CloudEventConstants.SpecVersion] = (ce, elem) => ce.SpecVersion = elem.GetString(),
            [CloudEventConstants.Id] = (ce, elem) => ce.Id = elem.GetString(),
            [CloudEventConstants.Source] = (ce, elem) => ce.Source = elem.GetString(),
            [CloudEventConstants.Type] = (ce, elem) => ce.Type = elem.GetString(),
            [CloudEventConstants.DataContentType] = (ce, elem) => ce.DataContentType = elem.GetString(),
            [CloudEventConstants.DataSchema] = (ce, elem) => ce.DataSchema = elem.ToString(),
            [CloudEventConstants.Subject] = (ce, elem) => ce.Subject = elem.GetString(),
            [CloudEventConstants.Time] = (ce, elem) => ce.Time = elem.GetString(),
            [CloudEventConstants.Data] =
                (ce, elem) =>
                {
                    ce.RawData = new BinaryData(elem);
                    ce.DataFormat = CloudEventDataFormat.Json;
                    ce.Data = elem.Clone();
                },
            [CloudEventConstants.DataBase64] =
                (ce, elem) =>
                {
                    if (elem.ValueKind == JsonValueKind.Null)
                    {
                        return;
                    }

                    BinaryData.FromBytes(elem.GetBytesFromBase64());
                    ce.DataFormat = CloudEventDataFormat.Binary;
                }
        };
    }

    public override CloudEvent Read(JsonElement rootElement)
    {
        var target = new CloudEvent();
        foreach (var property in rootElement.EnumerateObject())
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

        return target;
    }

    public override void Write(Utf8JsonWriter writer, CloudEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // These properties are required and thus assumed to be populated.
        // It is possible for them to be null if a CloudEvent was created by using Parse and passing
        // strict = false. However, we still will write the properties.
        writer.WritePropertyName(CloudEventConstants.Id);
        writer.WriteStringValue(value.Id);
        writer.WritePropertyName(CloudEventConstants.Source);
        writer.WriteStringValue(value.Source);
        writer.WritePropertyName(CloudEventConstants.Type);
        writer.WriteStringValue(value.Type);

        if (value.RawData != null)
        {
            switch (value.DataFormat)
            {
                case CloudEventDataFormat.Binary:
                    writer.WritePropertyName(CloudEventConstants.DataBase64);
                    writer.WriteBase64StringValue(value.RawData.ToArray());
                    break;
                case CloudEventDataFormat.Json:
                    using (var doc = JsonDocument.Parse(value.RawData.ToMemory()))
                    {
                        writer.WritePropertyName(CloudEventConstants.Data);
                        doc.RootElement.WriteTo(writer);
                        break;
                    }
            }
        }
        if (!string.IsNullOrWhiteSpace(value.Time))
        {
            writer.WritePropertyName(CloudEventConstants.Time);
            // unable to write an un-escaped string; need to parse and write as DateTimeOffset (https://github.com/dotnet/runtime/issues/28567)
            writer.WriteStringValue(DateTimeOffset.Parse(value.Time));
        }
        writer.WritePropertyName(CloudEventConstants.SpecVersion);
        writer.WriteStringValue(value.SpecVersion);
        if (value.DataSchema != null)
        {
            writer.WritePropertyName(CloudEventConstants.DataSchema);
            writer.WriteStringValue(value.DataSchema);
        }
        if (value.DataContentType != null)
        {
            writer.WritePropertyName(CloudEventConstants.DataContentType);
            writer.WriteStringValue(value.DataContentType);
        }
        if (value.Subject != null)
        {
            writer.WritePropertyName(CloudEventConstants.Subject);
            writer.WriteStringValue(value.Subject);
        }
        foreach (var item in value.ExtensionAttributes)
        {
            writer.WritePropertyName(item.Key);
            WriteObjectValue(writer, item.Value);
        }
        writer.WriteEndObject();
    }

    private static object GetObject(in JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                {
                    return intValue;
                }
                if (element.TryGetInt64(out var longValue))
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
                var dictionary = new Dictionary<string, object>();
                foreach (var jsonProperty in element.EnumerateObject())
                {
                    dictionary.Add(jsonProperty.Name, GetObject(jsonProperty.Value));
                }
                return dictionary;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(GetObject(item));
                }
                return list.ToArray();
            default:
                throw new NotSupportedException("Not supported value kind " + element.ValueKind);
        }
    }

    private static void WriteObjectValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case byte[] bytes:
                writer.WriteStringValue(Convert.ToBase64String(bytes));
                break;
            case ReadOnlyMemory<byte> rom:
                writer.WriteStringValue(Convert.ToBase64String(rom.ToArray()));
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case Guid g:
                writer.WriteStringValue(g);
                break;
            case Uri u:
                writer.WriteStringValue(u.ToString());
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset);
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime);
                break;
            case IEnumerable<KeyValuePair<string, object>> enumerable:
                writer.WriteStartObject();
                foreach (var pair in enumerable)
                {
                    writer.WritePropertyName(pair.Key);
                    WriteObjectValue(writer, pair.Value);
                }
                writer.WriteEndObject();
                break;
            case IEnumerable<object> objectEnumerable:
                writer.WriteStartArray();
                foreach (var item in objectEnumerable)
                {
                    WriteObjectValue(writer, item);
                }
                writer.WriteEndArray();
                break;

            default:
                throw new NotSupportedException("Not supported type " + value.GetType());
        }
    }
}
