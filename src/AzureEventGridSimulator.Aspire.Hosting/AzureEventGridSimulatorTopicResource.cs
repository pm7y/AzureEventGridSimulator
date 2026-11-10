namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     A topic on the Azure Event Grid Simulator. Referencing it gives an application the
///     connection string <c>Endpoint=https://localhost:{port}/api/events;Key={key}</c>, along with
///     separate <c>{NAME}_URI</c> and <c>{NAME}_KEY</c> environment variables. A container gets
///     the simulator's address on the container network in place of <c>localhost</c>.
/// </summary>
public sealed class AzureEventGridSimulatorTopicResource
    : Resource,
        IResourceWithParent<AzureEventGridSimulatorResource>,
        IResourceWithConnectionString
{
    private readonly List<SimulatorSubscriber> _subscribers = [];

    internal AzureEventGridSimulatorTopicResource(
        string name,
        int port,
        ParameterResource? key,
        AzureEventGridSimulatorResource parent
    )
        : base(name)
    {
        Port = port;
        Key = key;
        Parent = parent;
    }

    /// <summary>
    ///     Gets the HTTPS port the topic listens on.
    /// </summary>
    public int Port { get; }

    /// <summary>
    ///     Gets the parameter holding the topic's access key, or null when the topic has no key.
    /// </summary>
    public ParameterResource? Key { get; }

    /// <inheritdoc />
    public AzureEventGridSimulatorResource Parent { get; }

    /// <summary>
    ///     Gets the URL events are published to.
    /// </summary>
    public ReferenceExpression UriExpression =>
        ReferenceExpression.Create($"{new EndpointReference(Parent, Name)}/api/events");

    /// <inheritdoc />
    public ReferenceExpression ConnectionStringExpression =>
        Key is null
            ? ReferenceExpression.Create($"Endpoint={UriExpression}")
            : ReferenceExpression.Create($"Endpoint={UriExpression};Key={Key}");

    internal IReadOnlyList<SimulatorSubscriber> Subscribers => _subscribers;

    internal void AddSubscriber(SimulatorSubscriber subscriber) => _subscribers.Add(subscriber);

    IEnumerable<
        KeyValuePair<string, ReferenceExpression>
    > IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Uri", UriExpression);

        if (Key is not null)
        {
            yield return new("Key", ReferenceExpression.Create($"{Key}"));
        }
    }
}

/// <summary>
///     A subscriber, as the simulator's settings for one entry of a <c>subscribers</c> section.
/// </summary>
internal sealed record SimulatorSubscriber(
    string Section,
    IReadOnlyDictionary<string, object> Settings
);
