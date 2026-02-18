namespace AzureEventGridSimulator.Domain.Services.Retry;

/// <summary>
///     Calculates next retry time based on Azure Event Grid retry schedule.
/// </summary>
public class RetryScheduler
{
    /// <summary>
    ///     Azure Event Grid retry schedule with exponential backoff.
    /// </summary>
    private static readonly TimeSpan[] StandardSchedule =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12), // Then every 12h until TTL expires
    ];

    /// <summary>
    ///     HTTP status codes that indicate successful delivery.
    /// </summary>
    private static readonly int[] SuccessStatusCodes = [200, 201, 202, 203, 204];

    /// <summary>
    ///     HTTP status codes that should immediately dead-letter (no retry).
    /// </summary>
    private static readonly int[] ImmediateDeadLetterStatusCodes = [400, 401, 403, 413];

    private readonly TimeProvider _timeProvider;

    public RetryScheduler(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    ///     Calculates the next retry time based on attempt number and HTTP status code.
    /// </summary>
    /// <param name="attemptNumber">
    ///     The current attempt number (1-based).
    /// </param>
    /// <param name="httpStatusCode">
    ///     The HTTP status code from the failed attempt, if applicable.
    /// </param>
    /// <returns>
    ///     The next retry time.
    /// </returns>
    public DateTimeOffset GetNextRetryTime(int attemptNumber, int? httpStatusCode = null)
    {
        var delay = GetRetryDelay(attemptNumber, httpStatusCode);
        return _timeProvider.GetUtcNow().Add(delay);
    }

    /// <summary>
    ///     Gets the retry delay based on attempt number and HTTP status code.
    /// </summary>
    private static TimeSpan GetRetryDelay(int attemptNumber, int? httpStatusCode)
    {
        // Special timing for specific HTTP status codes (Azure Event Grid behavior)
        var minimumDelay = httpStatusCode switch
        {
            404 => TimeSpan.FromMinutes(5), // Not Found
            408 => TimeSpan.FromMinutes(2), // Request Timeout
            503 => TimeSpan.FromSeconds(30), // Service Unavailable
            _ => TimeSpan.Zero,
        };

        var standardDelay = GetStandardDelay(attemptNumber);

        // Use the larger of the two delays
        return minimumDelay > standardDelay ? minimumDelay : standardDelay;
    }

    /// <summary>
    ///     Gets the standard exponential backoff delay.
    /// </summary>
    private static TimeSpan GetStandardDelay(int attemptNumber)
    {
        // attemptNumber is 1-based, array is 0-based
        var index = attemptNumber - 1;

        if (index < 0)
        {
            return TimeSpan.Zero;
        }

        if (index < StandardSchedule.Length)
        {
            return StandardSchedule[index];
        }

        // After schedule exhausted, retry every 12 hours
        return TimeSpan.FromHours(12);
    }

    /// <summary>
    ///     Determines if an HTTP status code indicates successful delivery.
    /// </summary>
    /// <param name="statusCode">
    ///     The HTTP status code.
    /// </param>
    /// <returns>
    ///     True if the status code indicates success.
    /// </returns>
    public bool IsSuccessStatusCode(int statusCode)
    {
        return SuccessStatusCodes.Contains(statusCode);
    }

    /// <summary>
    ///     Determines if an HTTP status code should immediately dead-letter (no retry).
    /// </summary>
    /// <param name="statusCode">
    ///     The HTTP status code.
    /// </param>
    /// <returns>
    ///     True if the event should be immediately dead-lettered.
    /// </returns>
    public bool ShouldImmediatelyDeadLetter(int statusCode)
    {
        return ImmediateDeadLetterStatusCodes.Contains(statusCode);
    }

    /// <summary>
    ///     Gets the dead-letter reason for an HTTP status code.
    /// </summary>
    /// <param name="statusCode">
    ///     The HTTP status code.
    /// </param>
    /// <returns>
    ///     The dead-letter reason string.
    /// </returns>
    public string GetDeadLetterReasonForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            400 => "BadRequest",
            401 => "Unauthorized",
            403 => "Forbidden",
            413 => "PayloadTooLarge",
            _ => $"HttpStatus{statusCode}",
        };
    }
}
