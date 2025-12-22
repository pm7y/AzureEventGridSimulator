#nullable enable

namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
/// Represents a single delivery attempt to a subscriber.
/// </summary>
public class AttemptRecord
{
    /// <summary>
    /// 1-based attempt count.
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// When the attempt was made.
    /// </summary>
    public DateTimeOffset AttemptedAt { get; init; }

    /// <summary>
    /// Result of the attempt.
    /// </summary>
    public DeliveryOutcome Outcome { get; init; }

    /// <summary>
    /// HTTP status code if applicable.
    /// </summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>
    /// Error details if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates an AttemptRecord from an existing DeliveryAttempt.
    /// </summary>
    public static AttemptRecord FromDeliveryAttempt(DeliveryAttempt attempt)
    {
        return new AttemptRecord
        {
            AttemptNumber = attempt.AttemptNumber,
            AttemptedAt = attempt.AttemptTime,
            Outcome = attempt.Outcome,
            HttpStatusCode = attempt.HttpStatusCode,
            ErrorMessage = attempt.ErrorMessage,
        };
    }
}
