namespace AzureEventGridSimulator.Domain.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using AzureEventGridSimulator.Domain.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Properties of an event published to an Event Grid topic.
/// </summary>
[TypeConverter(typeof(EventGridEventConverter))]
[DataContract]
public class EventGridEvent : IEvent
{
    private static readonly IReadOnlyDictionary<string, Func<EventGridEvent, object>> _propertyAccessors;

    static EventGridEvent()
    {
        var accessors = new Dictionary<string, Func<EventGridEvent, object>>(StringComparer.OrdinalIgnoreCase);

        var typeExpression = Expression.Parameter(typeof(EventGridEvent));
        foreach (var pi in typeof(EventGridEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty))
        {
            var attrib = pi.GetCustomAttribute<DataMemberAttribute>();
            if (attrib == null)
            {
                continue;
            }

            Debug.Assert(!accessors.ContainsKey(attrib.Name));

            var exp = Expression.Lambda<Func<EventGridEvent, object>>(Expression.Property(typeExpression, pi), typeExpression).Compile();
            accessors.Add(attrib.Name, exp);
        }

        _propertyAccessors = accessors;
    }

    /// <summary>
    /// Gets or sets an unique identifier for the event.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.Id)]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets a resource path relative to the topic path.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.Subject)]
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets event data specific to the event type.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.Data)]
    public object Data { get; set; }

    /// <summary>
    /// Gets or sets the type of the event that occurred.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.EventType)]
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the time (in UTC) the event was generated.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.EventTime)]
    public string EventTime { get; set; }

    [JsonIgnore]
    private DateTime EventTimeParsed => DateTime.Parse(EventTime);

    [JsonIgnore]
    private bool EventTimeIsValid => DateTime.TryParse(EventTime, out _);

    /// <summary>
    /// Gets or sets the schema version of the data object.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.DataVersion)]
    public string DataVersion { get; set; }

    /// <summary>
    /// Gets the schema version of the event metadata.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.MetadataVersion)]
    public string MetadataVersion { get; set; }

    /// <summary>
    /// Gets or sets the resource path of the event source.
    /// </summary>
    [DataMember(Name = EventGridEventConstants.Topic)]
    public string Topic { get; set; }

    public bool TryGetValue(string key, out object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = null;
            return false;
        }

        if (key.Contains('.'))
        {
            var split = key.Split('.');
            if (string.Equals(split[0], EventGridEventConstants.Data, StringComparison.OrdinalIgnoreCase) && Data != null && split.Length > 1)
            {
                var tmpValue = Data;
                for (var i = 0; i < split.Length; i++)
                {
                    // look for the property on the grid event data object
                    if (tmpValue != null && JObject.FromObject(tmpValue).TryGetValue(split[i], out var dataValue))
                    {
                        tmpValue = dataValue.ToObject<object>();
                        if (i == split.Length - 1)
                        {
                            value = tmpValue;
                            return true;
                        }
                    }
                }
            }
        }

        if (_propertyAccessors.TryGetValue(key, out var expr))
        {
            value = expr(this);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Validate the object.
    /// </summary>
    /// <exception cref="InvalidOperationException" >
    /// Thrown if validation fails
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new JsonException($"This resource is configured for '{nameof(EventGridEvent)}' schema and requires '{EventGridEventConstants.Id}' property to be set.");
        }

        if (string.IsNullOrWhiteSpace(Subject))
        {
            throw new JsonException($"This resource is configured for '{nameof(EventGridEvent)}' schema and requires '{EventGridEventConstants.Subject}' property to be set.");
        }

        if (string.IsNullOrWhiteSpace(EventType))
        {
            throw new JsonException($"This resource is configured for '{nameof(EventGridEvent)}' schema and requires '{EventGridEventConstants.EventType}' property to be set.");
        }

        if (string.IsNullOrWhiteSpace(EventTime))
        {
            throw new JsonException($"This resource is configured for '{nameof(EventGridEvent)}' schema and requires '{EventGridEventConstants.EventTime}' property to be set.");
        }

        if (!EventTimeIsValid)
        {
            throw new JsonException($"The event time property '{EventGridEventConstants.EventTime}' was not a valid date/time.");
        }

        if (EventTimeParsed.Kind == DateTimeKind.Unspecified)
        {
            throw new JsonException($"Property '{EventGridEventConstants.EventTime}' must be either Local or UTC.");
        }

        if (MetadataVersion != null && MetadataVersion != "1")
        {
            throw new JsonException($"Property '{EventGridEventConstants.MetadataVersion}' was found to be set to '{MetadataVersion}', but was expected to either be null or be set to 1.");
        }

        if (!string.IsNullOrEmpty(Topic))
        {
            throw new JsonException($"Property '{EventGridEventConstants.Topic}' was found to be set to '{Topic}', but was expected to either be null/empty.");
        }
    }
}

internal static class EventGridEventConstants
{
    public const string Id = "id";
    public const string Subject = "subject";
    public const string Data = "data";
    public const string EventType = "eventType";
    public const string EventTime = "eventTime";
    public const string DataVersion = "dataVersion";
    public const string MetadataVersion = "metadataVersion";
    public const string Topic = "topic";
}
