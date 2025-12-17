using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Custom JSON converter that supports both legacy format (array of HTTP subscribers)
/// and new grouped format (object with http, serviceBus arrays).
/// </summary>
public class SubscribersSettingsConverter : JsonConverter<SubscribersSettings>
{
    public override SubscribersSettings Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new SubscribersSettings();
        }

        // Legacy format: array of HTTP subscribers
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var httpSubscribers = JsonSerializer.Deserialize<HttpSubscriberSettings[]>(
                ref reader,
                options
            );
            return new SubscribersSettings
            {
                Http = httpSubscribers ?? Array.Empty<HttpSubscriberSettings>(),
                ServiceBus = Array.Empty<ServiceBusSubscriberSettings>(),
                StorageQueue = Array.Empty<StorageQueueSubscriberSettings>(),
            };
        }

        // New format: object with http, serviceBus, storageQueue arrays
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var result = new SubscribersSettings();

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            if (root.TryGetProperty("http", out var httpElement))
            {
                result.Http =
                    JsonSerializer.Deserialize<HttpSubscriberSettings[]>(
                        httpElement.GetRawText(),
                        options
                    ) ?? Array.Empty<HttpSubscriberSettings>();
            }

            if (root.TryGetProperty("serviceBus", out var serviceBusElement))
            {
                result.ServiceBus =
                    JsonSerializer.Deserialize<ServiceBusSubscriberSettings[]>(
                        serviceBusElement.GetRawText(),
                        options
                    ) ?? Array.Empty<ServiceBusSubscriberSettings>();
            }

            if (root.TryGetProperty("storageQueue", out var storageQueueElement))
            {
                result.StorageQueue =
                    JsonSerializer.Deserialize<StorageQueueSubscriberSettings[]>(
                        storageQueueElement.GetRawText(),
                        options
                    ) ?? Array.Empty<StorageQueueSubscriberSettings>();
            }

            return result;
        }

        throw new JsonException(
            $"Unexpected token type '{reader.TokenType}' when parsing subscribers. Expected array or object."
        );
    }

    public override void Write(
        Utf8JsonWriter writer,
        SubscribersSettings value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();

        if (value.Http?.Length > 0)
        {
            writer.WritePropertyName("http");
            JsonSerializer.Serialize(writer, value.Http, options);
        }

        if (value.ServiceBus?.Length > 0)
        {
            writer.WritePropertyName("serviceBus");
            JsonSerializer.Serialize(writer, value.ServiceBus, options);
        }

        if (value.StorageQueue?.Length > 0)
        {
            writer.WritePropertyName("storageQueue");
            JsonSerializer.Serialize(writer, value.StorageQueue, options);
        }

        writer.WriteEndObject();
    }
}
