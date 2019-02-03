using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace AzureEventGridSimulator
{
    /// <summary>
    /// Properties of an event published to an Event Grid topic.
    /// </summary>
    [DataContract]
    public class EventGridEvent
    {
        /// <summary>
        /// Gets or sets an unique identifier for the event.
        /// </summary>
        [DataMember(Name = "id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets a resource path relative to the topic path.
        /// </summary>
        [DataMember(Name = "subject")]
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets event data specific to the event type.
        /// </summary>
        [DataMember(Name = "data")]
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets the type of the event that occurred.
        /// </summary>
        [DataMember(Name = "eventType")]
        public string EventType { get; set; }

        /// <summary>
        /// Gets or sets the time (in UTC) the event was generated.
        /// </summary>
        [DataMember(Name = "eventTime")]
        public string EventTime { get; set; }

        [JsonIgnore]
        public DateTime EventTimeParsed => DateTime.Parse(EventTime);

        [JsonIgnore]
        public bool EventTimeIsValid => DateTime.TryParse(EventTime, out _);

        /// <summary>
        /// Gets or sets the schema version of the data object.
        /// </summary>
        [DataMember(Name = "dataVersion")]
        public string DataVersion { get; set; }

        /// <summary>
        /// Gets the schema version of the event metadata.
        /// </summary>
        [DataMember(Name = "metadataVersion")]
        public string MetadataVersion { get; set; }

        /// <summary>
        /// Gets or sets the resource path of the event source.
        /// </summary>
        [DataMember(Name = "topic")]
        public string Topic { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="InvalidOperationException" >
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException($"Required property '{nameof(Id)}' was not set.");
            }

            if (string.IsNullOrWhiteSpace(Subject))
            {
                throw new InvalidOperationException($"Required property '{nameof(Subject)}' was not set.");
            }

            if (string.IsNullOrWhiteSpace(EventType))
            {
                throw new InvalidOperationException($"Required property '{nameof(EventType)}' was not set.");
            }

            if (string.IsNullOrWhiteSpace(EventTime))
            {
                throw new InvalidOperationException($"Required property '{nameof(EventTime)}' was not set.");
            }

            if (!EventTimeIsValid)
            {
                throw new InvalidOperationException($"The event time property '{nameof(EventTime)}' was not a valid date/time.");
            }

            if (EventTimeParsed.Kind == DateTimeKind.Unspecified)
            {
                throw new InvalidOperationException($"Property '{nameof(EventTime)}' must be either Local or UTC.");
            }

            if (MetadataVersion != null && MetadataVersion != "1")
            {
                throw new
                    InvalidOperationException($"Property '{nameof(MetadataVersion)}' was found to be set to '{MetadataVersion}', but was expected to either be null or be set to 1.");
            }

            if (Topic != null)
            {
                throw new InvalidOperationException($"Property '{nameof(Topic)}' was found to be set to '{Topic}', but was expected to either be null/empty.");
            }
        }
    }
}
