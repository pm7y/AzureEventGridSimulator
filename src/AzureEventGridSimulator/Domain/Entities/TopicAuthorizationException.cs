namespace AzureEventGridSimulator.Domain.Entities;

/// <summary>
///     Exception thrown when topic validation fails due to authorization issues.
///     Azure returns 401 when a client sets the topic field (which should be set by the service).
/// </summary>
public class TopicAuthorizationException : Exception
{
    public TopicAuthorizationException(string message)
        : base(message) { }
}
