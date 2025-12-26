namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
///     Records the outcome of a single delivery attempt.
/// </summary>
/// <param name="AttemptNumber">
///     The attempt number (1-based).
/// </param>
/// <param name="Outcome">
///     The outcome of the attempt.
/// </param>
/// <param name="AttemptTime">
///     The time of the attempt.
/// </param>
/// <param name="HttpStatusCode">
///     The HTTP status code if applicable.
/// </param>
/// <param name="ErrorMessage">
///     The error message if the attempt failed.
/// </param>
public record DeliveryAttempt(
    int AttemptNumber,
    DeliveryOutcome Outcome,
    DateTimeOffset AttemptTime,
    int? HttpStatusCode = null,
    string? ErrorMessage = null
)
{
    /// <summary>
    ///     Creates a new delivery attempt with the current UTC time.
    /// </summary>
    public DeliveryAttempt(int attemptNumber, DeliveryOutcome outcome)
        : this(attemptNumber, outcome, DateTimeOffset.UtcNow) { }
}
