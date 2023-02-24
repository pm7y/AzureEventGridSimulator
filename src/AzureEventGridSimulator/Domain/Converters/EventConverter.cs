namespace AzureEventGridSimulator.Domain.Converters
{
    using System;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using AzureEventGridSimulator.Domain.Entities;

    public abstract class EventConverter<TEvent> : JsonConverter<TEvent>
        where TEvent : IEvent
    {
        public static readonly string MaximumAllowedEventGridEventSizeErrorMesage = $"The maximum size for the JSON content({MaximumAllowedEventGridEventSizeInBytes}) has been exceeded.";

        private const int MaximumAllowedEventGridEventSizeInBytes = 1049600;

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(TEvent);
        }

        public override TEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(typeToConvert == typeof(TEvent));

            var start = reader.TokenStartIndex;

            var requestDocument = JsonDocument.ParseValue(ref reader);
            var target = Read(requestDocument.RootElement);

            var end = reader.TokenStartIndex;
            var length = end - start;

            if (length > MaximumAllowedEventGridEventSizeInBytes)
            {
                throw new JsonException(MaximumAllowedEventGridEventSizeErrorMesage);
            }

            target.Validate();

            return target;
        }

        public abstract TEvent Read(JsonElement rootElement);
    }
}
