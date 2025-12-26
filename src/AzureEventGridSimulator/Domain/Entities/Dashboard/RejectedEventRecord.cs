using System.Net;

namespace AzureEventGridSimulator.Domain.Entities.Dashboard;

/// <summary>
///     Represents an event that was rejected by the simulator (validation failure, parse error, etc.).
/// </summary>
public class RejectedEventRecord
{
    /// <summary>
    ///     Unique identifier for this rejection record.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     When the rejection occurred.
    /// </summary>
    public DateTimeOffset RejectedAt { get; init; }

    /// <summary>
    ///     Name of the topic that received the request.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    ///     Port the topic is listening on.
    /// </summary>
    public int TopicPort { get; init; }

    /// <summary>
    ///     HTTP status code returned to the client.
    /// </summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>
    ///     Error message returned to the client.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    ///     The raw request body (truncated if too large).
    /// </summary>
    public string? RawBody { get; init; }

    /// <summary>
    ///     Content-Type header from the request.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    ///     Creates a RejectedEventRecord from request context.
    /// </summary>
    public static RejectedEventRecord Create(
        string topicName,
        int topicPort,
        HttpStatusCode statusCode,
        string errorMessage,
        string? rawBody = null,
        string? contentType = null
    )
    {
        // Truncate raw body if too large (keep first 4KB for diagnostics)
        const int maxBodyLength = 4096;
        var truncatedBody =
            rawBody?.Length > maxBodyLength
                ? rawBody[..maxBodyLength] + "... [truncated]"
                : rawBody;

        return new RejectedEventRecord
        {
            Id = Guid.NewGuid().ToString(),
            RejectedAt = DateTimeOffset.UtcNow,
            TopicName = topicName,
            TopicPort = topicPort,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            RawBody = truncatedBody,
            ContentType = contentType,
        };
    }
}
