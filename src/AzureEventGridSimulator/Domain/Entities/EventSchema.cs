namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Defines the supported event schema types.
/// </summary>
public enum EventSchema
{
    /// <summary>
    /// Azure Event Grid schema (default).
    /// </summary>
    EventGridSchema,

    /// <summary>
    /// CloudEvents v1.0 schema.
    /// </summary>
    CloudEventV1_0
}
