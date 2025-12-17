namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
/// Records the outcome of a single delivery attempt.
/// </summary>
public class DeliveryAttempt
{
    /// <summary>
    /// Gets or sets the time of the attempt.
    /// </summary>
    public DateTime AttemptTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Gets or sets the outcome of the attempt.
    /// </summary>
    public DeliveryOutcome Outcome { get; init; }

    /// <summary>
    /// Gets or sets the HTTP status code if applicable.
    /// </summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>
    /// Gets or sets the error message if the attempt failed.
    /// </summary>
    public string ErrorMessage { get; init; }
}
