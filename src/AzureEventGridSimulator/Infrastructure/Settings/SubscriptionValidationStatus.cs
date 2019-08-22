namespace AzureEventGridSimulator.Infrastructure.Settings
{
    public enum SubscriptionValidationStatus
    {
        None = 0,
        ValidationEventSent = 1,
        AwaitingValidationCode = 2,
        ValidationFailed = 3,
        ValidationSuccessful = 4
    }
}