using System;
using System.Runtime.Serialization;

namespace AzureEventGridSimulator
{
    /// <summary>
    /// Properties of an event published to an Event Grid topic.
    /// </summary>
    [DataContract]
    public class EventGridEvent
    {
        /// <summary>Gets or sets an unique identifier for the event.</summary>
        [DataMember(Name = "id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets a resource path relative to the topic path.
        /// </summary>
        [DataMember(Name = "subject")]
        public string Subject { get; set; }

        /// <summary>Gets or sets event data specific to the event type.</summary>
        [DataMember(Name = "data")]
        public object Data { get; set; }

        /// <summary>Gets or sets the type of the event that occurred.</summary>
        [DataMember(Name = "eventType")]
        public string EventType { get; set; }

        /// <summary>
        /// Gets or sets the time (in UTC) the event was generated.
        /// </summary>
        [DataMember(Name = "eventTime")]
        public DateTime EventTime { get; set; }

        /// <summary>Gets or sets the schema version of the data object.</summary>
        [DataMember(Name = "dataVersion")]
        public string DataVersion { get; set; }

        /// <summary>Gets the schema version of the event metadata.</summary>
        [DataMember(Name = "metadataVersion")]
        public string MetadataVersion { get; set; }

        /// <summary>Gets or sets the resource path of the event source.</summary>
        [DataMember(Name = "topic")]
        public string Topic { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if validation fails</exception>
        public virtual void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException($"{nameof(Id)} must not be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Subject))
            {
                throw new InvalidOperationException($"{nameof(Subject)} must not be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(EventType))
            {
                throw new InvalidOperationException($"{nameof(EventType)} must not be null or whitespace.");
            }

            if (EventTime.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException($"{nameof(EventTime)} must be UTC.");
            }

            if (string.IsNullOrWhiteSpace(DataVersion))
            {
                throw new InvalidOperationException($"{nameof(DataVersion)} must not be null or whitespace.");
            }

            if (MetadataVersion != null)
            {
                throw new InvalidOperationException($"{nameof(MetadataVersion)} should be null.");
            }

            if (Topic != null)
            {
                throw new InvalidOperationException($"{nameof(Topic)} should be null.");
            }
        }
    }
}
