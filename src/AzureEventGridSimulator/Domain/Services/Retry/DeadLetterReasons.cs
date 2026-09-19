namespace AzureEventGridSimulator.Domain.Services.Retry;

/// <summary>
///     The reasons written to the <c>deadLetterReason</c> field of a dead-letter file. They are
///     output that users can match on, so don't change the values.
/// </summary>
internal static class DeadLetterReasons
{
    /// <summary>
    ///     The event's time-to-live ran out, either before an attempt or during a failed one.
    /// </summary>
    public const string EventTimeToLiveExpired = "EventTimeToLiveExpired";

    /// <summary>
    ///     The subscriber's maximum number of delivery attempts has been used up.
    /// </summary>
    public const string MaxDeliveryAttemptsExceeded = "MaxDeliveryAttemptsExceeded";

    /// <summary>
    ///     A delivery attempt failed and the subscriber's retry policy is disabled.
    /// </summary>
    public const string RetryDisabledDeliveryFailed = "RetryDisabled_DeliveryFailed";

    /// <summary>
    ///     The endpoint returned HTTP 400, which is never retried.
    /// </summary>
    public const string BadRequest = "BadRequest";

    /// <summary>
    ///     The endpoint returned HTTP 401, which is never retried.
    /// </summary>
    public const string Unauthorized = "Unauthorized";

    /// <summary>
    ///     The endpoint returned HTTP 403, which is never retried.
    /// </summary>
    public const string Forbidden = "Forbidden";

    /// <summary>
    ///     The endpoint returned HTTP 413, which is never retried.
    /// </summary>
    public const string PayloadTooLarge = "PayloadTooLarge";
}
