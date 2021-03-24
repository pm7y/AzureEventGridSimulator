namespace AzureEventGridSimulator.Infrastructure.Settings
{
    public enum SubscriptionValidationStatus
    {
        None = 0,
        ValidationEventSent = 1,
        ValidationFailed = 2,
        ValidationSuccessful = 3
    }
}
