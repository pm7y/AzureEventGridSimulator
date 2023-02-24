namespace AzureEventGridSimulator.Domain.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using AzureEventGridSimulator.Domain.Commands;
    using AzureEventGridSimulator.Domain.Entities;

    public class ServiceBusMessageConverter<TEvent> : JsonConverter<ServiceBusMessage<TEvent>>
        where TEvent : IEvent
    {
        private readonly IReadOnlyDictionary<string, Action<ServiceBusMessage<TEvent>, JsonElement>> _propertyMap;

        public ServiceBusMessageConverter(EventConverter<TEvent> eventConverter)
        {
            _propertyMap = new Dictionary<string, Action<ServiceBusMessage<TEvent>, JsonElement>>
            {
                [ServiceBusMessageConstants.Body] = (msg, elem) => msg.Body = eventConverter.Read(elem),
                [ServiceBusMessageConstants.BrokerProperties] = (msg, elem) => msg.BrokerProperties = elem.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString()),
                [ServiceBusMessageConstants.UserProperties] = (msg, elem) => msg.UserProperties = elem.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString())
            };

            EventConverter = eventConverter;
        }

        public EventConverter<TEvent> EventConverter { get; }

        public override ServiceBusMessage<TEvent> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(typeToConvert == typeof(ServiceBusMessage<TEvent>));

            var requestDocument = JsonDocument.ParseValue(ref reader);
            var target = new ServiceBusMessage<TEvent>();
            foreach (var property in requestDocument.RootElement.EnumerateObject())
            {
                if (_propertyMap.TryGetValue(property.Name, out var value))
                {
                    value(target, property.Value);
                }
            }

            return target;
        }

        public override void Write(Utf8JsonWriter writer, ServiceBusMessage<TEvent> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(ServiceBusMessageConstants.Body);
            EventConverter.Write(writer, value.Body, null);

            if (value.BrokerProperties.Count > 0)
            {
                writer.WritePropertyName(ServiceBusMessageConstants.BrokerProperties);
                writer.WriteStartObject();

                foreach (var property in value.BrokerProperties)
                {
                    writer.WritePropertyName(property.Key);
                    writer.WriteStringValue(property.Value);
                }

                writer.WriteEndObject();
            }

            if (value.UserProperties.Count > 0)
            {
                writer.WritePropertyName(ServiceBusMessageConstants.UserProperties);
                writer.WriteStartObject();

                foreach (var property in value.UserProperties)
                {
                    writer.WritePropertyName(property.Key);
                    writer.WriteStringValue(property.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static class ServiceBusMessageConstants
        {
            public static string Body = "Body";
            public static string BrokerProperties = "BrokerProperties";
            public static string UserProperties = "UserProperties";
        }
    }
}
