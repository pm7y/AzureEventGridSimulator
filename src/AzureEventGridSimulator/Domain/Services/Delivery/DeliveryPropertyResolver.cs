using System;
using System.Collections.Generic;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Newtonsoft.Json.Linq;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
/// Resolves static and dynamic delivery properties for Service Bus messages.
/// </summary>
public class DeliveryPropertyResolver
{
    /// <summary>
    /// Resolves all delivery properties for a given event.
    /// </summary>
    /// <param name="properties">The property configurations.</param>
    /// <param name="evt">The event to extract dynamic property values from.</param>
    /// <returns>A dictionary of resolved property names and values.</returns>
    public Dictionary<string, object> ResolveProperties(
        Dictionary<string, DeliveryPropertySettings> properties,
        SimulatorEvent evt
    )
    {
        var result = new Dictionary<string, object>();

        if (properties == null)
        {
            return result;
        }

        foreach (var (name, setting) in properties)
        {
            var value = ResolveProperty(setting, evt);
            if (value != null)
            {
                result[name] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves a single delivery property value.
    /// </summary>
    /// <param name="setting">The property configuration.</param>
    /// <param name="evt">The event to extract dynamic property values from.</param>
    /// <returns>The resolved property value, or null if not found.</returns>
    public object ResolveProperty(DeliveryPropertySettings setting, SimulatorEvent evt)
    {
        if (setting == null)
        {
            return null;
        }

        if (setting.IsStatic)
        {
            return setting.Value;
        }

        if (setting.IsDynamic)
        {
            return GetValueFromEvent(evt, setting.Value);
        }

        return null;
    }

    /// <summary>
    /// Gets a value from an event using a property path (e.g., "Subject", "data.customerId").
    /// Uses the same property access pattern as the filter extensions.
    /// </summary>
    private static object GetValueFromEvent(SimulatorEvent evt, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Handle top-level properties
        switch (path)
        {
            case "Id":
            case "id":
                return evt.Id;
            case "Topic":
            case "topic":
            case "Source":
            case "source":
                return evt.Source;
            case "Subject":
            case "subject":
                return evt.Subject;
            case "EventType":
            case "eventType":
            case "Type":
            case "type":
                return evt.EventType;
            case "DataVersion":
            case "dataVersion":
            case "DataSchema":
            case "dataschema":
                return evt.DataVersion;
            case "EventTime":
            case "eventTime":
            case "Time":
            case "time":
                return evt.EventTime;
            case "Data":
            case "data":
                return evt.Data;
        }

        // Handle nested data properties (e.g., "data.customerId" or "Data.order.id")
        var split = path.Split('.');
        if ((split[0] == "Data" || split[0] == "data") && evt.Data != null && split.Length > 1)
        {
            return GetNestedValue(evt.Data, split, 1);
        }

        return null;
    }

    /// <summary>
    /// Gets a nested value from an object using a property path.
    /// </summary>
    private static object GetNestedValue(object data, string[] pathParts, int startIndex)
    {
        try
        {
            // Convert the data object to a JToken for navigation
            JToken current = JToken.FromObject(data);

            for (var i = startIndex; i < pathParts.Length; i++)
            {
                if (current == null || current.Type == JTokenType.Null)
                {
                    return null;
                }

                if (current is not JObject jObject)
                {
                    return null;
                }

                if (
                    !jObject.TryGetValue(
                        pathParts[i],
                        StringComparison.OrdinalIgnoreCase,
                        out var token
                    )
                )
                {
                    return null;
                }

                current = token;
            }

            // Convert the final JToken to an appropriate .NET type
            return ConvertJToken(current);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a JToken to an appropriate .NET type for use as a Service Bus message property.
    /// </summary>
    private static object ConvertJToken(JToken token)
    {
        return token.Type switch
        {
            JTokenType.String => token.Value<string>(),
            JTokenType.Integer => token.Value<long>(),
            JTokenType.Float => token.Value<double>(),
            JTokenType.Boolean => token.Value<bool>(),
            JTokenType.Date => token.Value<DateTime>(),
            JTokenType.Guid => token.Value<Guid>(),
            JTokenType.Null => null,
            _ => token.ToString(),
        };
    }
}
