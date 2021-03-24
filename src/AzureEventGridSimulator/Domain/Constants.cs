namespace AzureEventGridSimulator.Domain
{
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
    }
}
