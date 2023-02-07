namespace AzureEventGridSimulator.Domain.Converters;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;

internal class EventGridEventConverter : JsonConverter<EventGridEvent>
{
    public static readonly string MaximumAllowedEventGridEventSizeErrorMesage = $"The maximum size for the JSON content({MaximumAllowedEventGridEventSizeInBytes}) has been exceeded.";

    private const int MaximumAllowedEventGridEventSizeInBytes = 1049600;
    private static readonly IReadOnlyDictionary<string, Action<EventGridEvent, JsonElement>> _propertyMap;

    static EventGridEventConverter()
    {
        _propertyMap = new Dictionary<string, Action<EventGridEvent, JsonElement>>
        {
            [EventGridEventConstants.Id] = (ege, elem) => ege.Id = elem.GetString(),
            [EventGridEventConstants.Topic] = (ege, elem) => ege.Topic = elem.GetString(),
            [EventGridEventConstants.Subject] = (ege, elem) => ege.Subject = elem.GetString(),
            [EventGridEventConstants.Data] = (ege, elem) => ege.Data = elem.Clone(),
            [EventGridEventConstants.EventType] = (ege, elem) => ege.EventType = elem.GetString(),
            [EventGridEventConstants.EventTime] = (ege, elem) => ege.EventTime = elem.GetDateTimeOffset("O").ToUniversalTime().ToString(),
            [EventGridEventConstants.MetadataVersion] = (ege, elem) => ege.MetadataVersion = elem.GetString(),
            [EventGridEventConstants.DataVersion] = (ege, elem) => ege.DataVersion = elem.GetString()
        };
    }

    public override EventGridEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(typeToConvert == typeof(EventGridEvent));

        var start = reader.TokenStartIndex;

        var requestDocument = JsonDocument.ParseValue(ref reader);
        var target = new EventGridEvent();
        foreach (var property in requestDocument.RootElement.EnumerateObject())
        {
            if (_propertyMap.TryGetValue(property.Name, out var value))
            {
                value(target, property.Value);
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

    public override void Write(Utf8JsonWriter writer, EventGridEvent value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
