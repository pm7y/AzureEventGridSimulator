namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
///     A unified event wrapper that can hold either an EventGridEvent or CloudEvent.
///     Provides common accessors for event properties regardless of the underlying schema.
/// </summary>
public class SimulatorEvent
{
    /// <summary>
    ///     Gets or sets the schema type of the event.
    /// </summary>
    public EventSchema Schema { get; set; }

    /// <summary>
    ///     Gets or sets the underlying EventGridEvent (when Schema is EventGridSchema).
    /// </summary>
    public EventGridEvent? EventGridEvent { get; set; }

    /// <summary>
    ///     Gets or sets the underlying CloudEvent (when Schema is CloudEventV1_0).
    /// </summary>
    public CloudEvent? CloudEvent { get; set; }

    /// <summary>
    ///     Gets the unique event identifier.
    /// </summary>
    public string Id =>
        Schema switch
        {
            EventSchema.EventGridSchema => EventGridEvent?.Id
                ?? throw new InvalidOperationException("EventGridEvent is null"),
            EventSchema.CloudEventV1_0 => CloudEvent?.Id
                ?? throw new InvalidOperationException("CloudEvent is null"),
            _ => throw new InvalidOperationException($"Unknown schema: {Schema}"),
        };

    /// <summary>
    ///     Gets the event subject.
    ///     For CloudEvents, falls back to source if subject is not set.
    ///     Azure is lenient and accepts events without subject or source.
    /// </summary>
    public string Subject =>
        Schema switch
        {
            EventSchema.EventGridSchema => EventGridEvent?.Subject
                ?? throw new InvalidOperationException("EventGridEvent is null"),
            EventSchema.CloudEventV1_0 => CloudEvent?.Subject ?? CloudEvent?.Source ?? "",
            _ => throw new InvalidOperationException($"Unknown schema: {Schema}"),
        };

    /// <summary>
    ///     Gets the event type.
    /// </summary>
    public string EventType =>
        Schema switch
        {
            EventSchema.EventGridSchema => EventGridEvent?.EventType
                ?? throw new InvalidOperationException("EventGridEvent is null"),
            EventSchema.CloudEventV1_0 => CloudEvent?.Type
                ?? throw new InvalidOperationException("CloudEvent is null"),
            _ => throw new InvalidOperationException($"Unknown schema: {Schema}"),
        };

    /// <summary>
    ///     Gets the event timestamp.
    /// </summary>
    public string? EventTime =>
        Schema switch
        {
            EventSchema.EventGridSchema => EventGridEvent?.EventTime,
            EventSchema.CloudEventV1_0 => CloudEvent?.Time,
            _ => throw new InvalidOperationException($"Unknown schema: {Schema}"),
        };

    /// <summary>
    ///     Gets the event data payload.
    /// </summary>
    public object? Data =>
        Schema switch
        {
            EventSchema.EventGridSchema => EventGridEvent?.Data,
            EventSchema.CloudEventV1_0 => CloudEvent?.Data,
            _ => throw new InvalidOperationException($"Unknown schema: {Schema}"),
        };

    /// <summary>
    ///     Gets the event source/topic.
    /// </summary>
    public string? Source =>
        Schema switch
        {
            EventSchema.EventGridSchema => EventGridEvent?.Topic,
            EventSchema.CloudEventV1_0 => CloudEvent?.Source,
            _ => throw new InvalidOperationException($"Unknown schema: {Schema}"),
        };

    /// <summary>
    ///     Gets the data version/schema.
    /// </summary>
    public string? DataVersion =>
        Schema switch
        {
            EventSchema.EventGridSchema => EventGridEvent?.DataVersion,
            EventSchema.CloudEventV1_0 => CloudEvent?.DataSchema,
            _ => throw new InvalidOperationException($"Unknown schema: {Schema}"),
        };

    /// <summary>
    ///     Creates a SimulatorEvent from an EventGridEvent.
    /// </summary>
    public static SimulatorEvent FromEventGridEvent(EventGridEvent evt)
    {
        return new SimulatorEvent { Schema = EventSchema.EventGridSchema, EventGridEvent = evt };
    }

    /// <summary>
    ///     Creates a SimulatorEvent from a CloudEvent.
    /// </summary>
    public static SimulatorEvent FromCloudEvent(CloudEvent evt)
    {
        return new SimulatorEvent { Schema = EventSchema.CloudEventV1_0, CloudEvent = evt };
    }

    /// <summary>
    ///     Validates the underlying event based on its schema.
    /// </summary>
    public void Validate()
    {
        switch (Schema)
        {
            case EventSchema.EventGridSchema:
                EventGridEvent?.Validate();
                break;
            case EventSchema.CloudEventV1_0:
                CloudEvent?.Validate();
                break;
            default:
                throw new InvalidOperationException($"Unknown schema: {Schema}");
        }
    }
}
