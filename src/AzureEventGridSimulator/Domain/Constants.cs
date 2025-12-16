namespace AzureEventGridSimulator.Domain;

public static class Constants
{
    // Headers
    public const string AegSasTokenHeader = "aeg-sas-token";
    public const string AegSasKeyHeader = "aeg-sas-key";
    public const string AegEventTypeHeader = "aeg-event-type";
    public const string AegSubscriptionNameHeader = "aeg-subscription-name";
    public const string AegDataVersionHeader = "aeg-data-version";
    public const string AegMetadataVersionHeader = "aeg-metadata-version";
    public const string AegDeliveryCountHeader = "aeg-delivery-count";

    // Event Types
    public const string NotificationEventType = "Notification";
    public const string ValidationEventType = "SubscriptionValidation";

    // Other
    public const string SupportedApiVersion = "2018-01-01";
    public const string SasAuthorizationType = "SharedAccessSignature";

    // CloudEvents Headers (binary mode)
    public const string CeSpecVersionHeader = "ce-specversion";
    public const string CeTypeHeader = "ce-type";
    public const string CeSourceHeader = "ce-source";
    public const string CeIdHeader = "ce-id";
    public const string CeTimeHeader = "ce-time";
    public const string CeSubjectHeader = "ce-subject";
    public const string CeDataContentTypeHeader = "ce-datacontenttype";
    public const string CeDataSchemaHeader = "ce-dataschema";

    // CloudEvents Content Types (base types for detection)
    public const string CloudEventsContentTypeBase = "application/cloudevents+json";
    public const string CloudEventsBatchContentTypeBase = "application/cloudevents-batch+json";

    // CloudEvents Content Types with charset (for output per Azure spec)
    public const string CloudEventsContentType = "application/cloudevents+json; charset=utf-8";
    public const string CloudEventsBatchContentType =
        "application/cloudevents-batch+json; charset=utf-8";
}
