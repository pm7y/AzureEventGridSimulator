namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
///     Defines the supported event schema types.
/// </summary>
public enum EventSchema
{
    /// <summary>
    ///     Azure Event Grid schema (default).
    /// </summary>
    EventGridSchema,

    /// <summary>
    ///     CloudEvents v1.0 schema.
    /// </summary>
    // Member names are public config values (inputSchema/outputSchema/deliverySchema)
    // and appear in the dashboard; do not rename.
    // ReSharper disable once InconsistentNaming
    CloudEventV1_0,
}
