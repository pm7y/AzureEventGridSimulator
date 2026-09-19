namespace AzureEventGridSimulator.Domain.Entities;

public static class EventSchemaExtensions
{
    extension(EventSchema schema)
    {
        /// <summary>
        ///     Gets the name Azure gives the schema in error messages and in the
        ///     aeg-input-event-schema header.
        /// </summary>
        public string ToAzureSchemaName()
        {
            return schema == EventSchema.CloudEventV1_0 ? "CloudEventV10" : "EventGridEvent";
        }
    }
}
