namespace AzureEventGridSimulator.Domain.Converters;

using System;
using System.Collections.Generic;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;

internal class EventGridEventConverter : EventConverter<EventGridEvent>
{
    private static readonly IReadOnlyDictionary<string, Action<EventGridEvent, JsonElement>> _propertyMap;

    static EventGridEventConverter()
    {
        _propertyMap = new Dictionary<string, Action<EventGridEvent, JsonElement>>
        {
            [EventGridEventConstants.Id] = (ege, elem) => ege.Id = elem.GetString(),
            [EventGridEventConstants.Topic] = (ege, elem) => ege.Topic = elem.GetString(),
            [EventGridEventConstants.Subject] = (ege, elem) => ege.Subject = elem.GetString(),
            [EventGridEventConstants.Data] =
                (ege, elem) =>
                {
                    ege.Data = elem.Clone();
                    ege.RawData = new BinaryData(elem);
                },
            [EventGridEventConstants.EventType] = (ege, elem) => ege.EventType = elem.GetString(),
            [EventGridEventConstants.EventTime] = (ege, elem) => ege.EventTime = elem.GetDateTimeOffset("O").ToUniversalTime().ToString(),
            [EventGridEventConstants.MetadataVersion] = (ege, elem) => ege.MetadataVersion = elem.GetString(),
            [EventGridEventConstants.DataVersion] = (ege, elem) => ege.DataVersion = elem.GetString()
        };
    }

    public override EventGridEvent Read(JsonElement rootElement)
    {
        var target = new EventGridEvent();
        foreach (var property in rootElement.EnumerateObject())
        {
            if (_propertyMap.TryGetValue(property.Name, out var value))
            {
                value(target, property.Value);
            }
        }

        return target;
    }

    public override void Write(Utf8JsonWriter writer, EventGridEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(EventGridEventConstants.Topic);
        writer.WriteStringValue(value.Topic);

        writer.WritePropertyName(EventGridEventConstants.Subject);
        writer.WriteStringValue(value.Subject);

        writer.WritePropertyName(EventGridEventConstants.EventType);
        writer.WriteStringValue(value.EventType);

        if (value.EventTimeIsValid)
        {
            writer.WritePropertyName(EventGridEventConstants.EventTime);
            writer.WriteStringValue(value.EventTimeParsed);
        }

        writer.WritePropertyName(EventGridEventConstants.Id);
        writer.WriteStringValue(value.Id);

        using (var doc = JsonDocument.Parse(value.RawData.ToMemory()))
        {
            writer.WritePropertyName(EventGridEventConstants.Data);
            doc.RootElement.WriteTo(writer);
        }

        writer.WritePropertyName(EventGridEventConstants.DataVersion);
        writer.WriteStringValue(value.DataVersion);

        writer.WritePropertyName(EventGridEventConstants.MetadataVersion);
        writer.WriteStringValue(value.MetadataVersion);

        writer.WriteEndObject();
    }
}
