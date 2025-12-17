using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Custom JSON converter that supports both legacy format (array of HTTP subscribers)
/// and new grouped format (object with http, serviceBus arrays).
/// </summary>
public class SubscribersSettingsConverter : JsonConverter<SubscribersSettings>
{
    public override SubscribersSettings ReadJson(
        JsonReader reader,
        Type objectType,
        SubscribersSettings existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        var token = JToken.Load(reader);

        if (token.Type == JTokenType.Null)
        {
            return new SubscribersSettings();
        }

        // Legacy format: array of HTTP subscribers
        if (token.Type == JTokenType.Array)
        {
            var httpSubscribers = token.ToObject<HttpSubscriberSettings[]>(serializer);
            return new SubscribersSettings
            {
                Http = httpSubscribers ?? Array.Empty<HttpSubscriberSettings>(),
                ServiceBus = Array.Empty<ServiceBusSubscriberSettings>(),
            };
        }

        // New format: object with http, serviceBus arrays
        if (token.Type == JTokenType.Object)
        {
            var result = new SubscribersSettings();

            var httpToken = token["http"];
            if (httpToken != null)
            {
                result.Http =
                    httpToken.ToObject<HttpSubscriberSettings[]>(serializer)
                    ?? Array.Empty<HttpSubscriberSettings>();
            }

            var serviceBusToken = token["serviceBus"];
            if (serviceBusToken != null)
            {
                result.ServiceBus =
                    serviceBusToken.ToObject<ServiceBusSubscriberSettings[]>(serializer)
                    ?? Array.Empty<ServiceBusSubscriberSettings>();
            }

            return result;
        }

        throw new JsonSerializationException(
            $"Unexpected token type '{token.Type}' when parsing subscribers. Expected array or object."
        );
    }

    public override void WriteJson(
        JsonWriter writer,
        SubscribersSettings value,
        JsonSerializer serializer
    )
    {
        // Always write in the new format
        var obj = new JObject();

        if (value.Http?.Length > 0)
        {
            obj["http"] = JArray.FromObject(value.Http, serializer);
        }

        if (value.ServiceBus?.Length > 0)
        {
            obj["serviceBus"] = JArray.FromObject(value.ServiceBus, serializer);
        }

        obj.WriteTo(writer);
    }
}
