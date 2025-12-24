namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
/// Represents a single delivery attempt to a subscriber.
/// </summary>
/// <param name="AttemptNumber" >
/// 1-based attempt count.
/// </param>
/// <param name="AttemptedAt" >
/// When the attempt was made.
/// </param>
/// <param name="Outcome" >
/// Result of the attempt.
/// </param>
/// <param name="HttpStatusCode" >
/// HTTP status code if applicable.
/// </param>
/// <param name="ErrorMessage" >
/// Error details if failed.
/// </param>
public record AttemptRecord(
    int AttemptNumber,
    DateTimeOffset AttemptedAt,
    DeliveryOutcome Outcome,
    int? HttpStatusCode = null,
    string? ErrorMessage = null
)
{
    /// <summary>
    /// Creates an AttemptRecord from an existing DeliveryAttempt.
    /// </summary>
    public static AttemptRecord FromDeliveryAttempt(DeliveryAttempt attempt)
    {
        return new AttemptRecord(
            attempt.AttemptNumber,
            attempt.AttemptTime,
            attempt.Outcome,
            attempt.HttpStatusCode,
            attempt.ErrorMessage
        );
    }
}
