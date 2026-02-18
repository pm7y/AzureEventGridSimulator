using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Interface for formatting events for delivery to subscribers.
/// </summary>
public interface IEventSchemaFormatter
{
    /// <summary>
    ///     Gets the schema type this formatter produces.
    /// </summary>
    EventSchema Schema { get; }

    /// <summary>
    ///     Gets the Content-Type header value for this schema.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    ///     Serializes an event to JSON for delivery.
    ///     By default, wraps single events in an array for Azure Event Grid compatibility.
    /// </summary>
    /// <param name="evt">
    ///     The event to serialize.
    /// </param>
    /// <returns>
    ///     The JSON representation of the event.
    /// </returns>
    string Serialize(SimulatorEvent evt);

    /// <summary>
    ///     Serializes a single event to JSON without array wrapper.
    ///     Used for Service Bus delivery which doesn't use array format.
    /// </summary>
    /// <param name="evt">
    ///     The event to serialize.
    /// </param>
    /// <returns>
    ///     The JSON representation of the single event.
    /// </returns>
    string SerializeSingle(SimulatorEvent evt);

    /// <summary>
    ///     Serializes multiple events to JSON for delivery.
    /// </summary>
    /// <param name="events">
    ///     The events to serialize.
    /// </param>
    /// <returns>
    ///     The JSON representation of the events.
    /// </returns>
    string SerializeArray(IEnumerable<SimulatorEvent> events);

    /// <summary>
    ///     Gets the HTTP headers to include with the delivery.
    /// </summary>
    /// <param name="evt">
    ///     The event being delivered.
    /// </param>
    /// <returns>
    ///     Dictionary of header names and values.
    /// </returns>
    Dictionary<string, string> GetHeaders(SimulatorEvent evt);
}
