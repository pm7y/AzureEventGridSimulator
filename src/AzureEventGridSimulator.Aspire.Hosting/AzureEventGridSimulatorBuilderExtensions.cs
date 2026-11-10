using System.Reflection;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
///     Adds the Azure Event Grid Simulator, its topics and their subscribers to an Aspire AppHost.
/// </summary>
public static class AzureEventGridSimulatorBuilderExtensions
{
    private const string HealthPath = "/api/health";

    internal const string CertificatePathVariable =
        "ASPNETCORE_Kestrel__Certificates__Default__Path";
    internal const string CertificatePasswordVariable =
        "ASPNETCORE_Kestrel__Certificates__Default__Password";

    // What MSBuild stamps on a build that wasn't given a version, so no published image has it
    private const string UnversionedBuild = "1.0.0";
    private const string LatestTag = "latest";

    /// <summary>
    ///     Adds the simulator, which runs as a container from its published image. It serves
    ///     HTTPS with the ASP.NET Core developer certificate, so run
    ///     <c>dotnet dev-certs https --trust</c> once. Local development only.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A builder for the simulator. Call <see cref="AddTopic" /> to give it topics.</returns>
    public static IResourceBuilder<AzureEventGridSimulatorResource> AddAzureEventGridSimulator(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resource = new AzureEventGridSimulatorResource(name);

        builder.Eventing.Subscribe<BeforeStartEvent>(
            (_, _) =>
            {
                EnsureHasTopics(resource);
                return Task.CompletedTask;
            }
        );

        // The simulator only loads a certificate that has a password. The parameter isn't added
        // to the model, so it doesn't show up as a resource of its own.
        var certificatePassword = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(
            builder,
            $"{name}-certificate-password"
        );

        return builder
            .AddResource(resource)
            .WithImage(AzureEventGridSimulatorResource.Image, GetMatchingImageTag())
            .WithImageRegistry(AzureEventGridSimulatorResource.Registry)
            .WithHttpsDeveloperCertificate(builder.CreateResourceBuilder(certificatePassword))
            .WithHttpsCertificateConfiguration(context =>
            {
                context.EnvironmentVariables[CertificatePathVariable] = context.PfxPath;

                if (context.Password is { } password)
                {
                    context.EnvironmentVariables[CertificatePasswordVariable] = password;
                }

                return Task.CompletedTask;
            })
            .WithEnvironment(resource.WriteConfiguration)
            .ExcludeFromManifest();
    }

    /// <summary>
    ///     Adds a topic, listening over HTTPS on a fixed port.
    /// </summary>
    /// <param name="builder">The simulator.</param>
    /// <param name="name">The name of both the resource and the simulator's topic.</param>
    /// <param name="port">The port, both on the host and inside the container.</param>
    /// <param name="key">The topic's access key. Without one, the topic accepts unauthenticated events.</param>
    /// <returns>A builder for the topic.</returns>
    public static IResourceBuilder<AzureEventGridSimulatorTopicResource> AddTopic(
        this IResourceBuilder<AzureEventGridSimulatorResource> builder,
        [ResourceName] string name,
        int port,
        IResourceBuilder<ParameterResource>? key = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        var simulator = builder.Resource;
        if (simulator.Topics.Any(t => t.Port == port))
        {
            throw new DistributedApplicationException(
                $"Port {port} is already used by another topic on the simulator '{simulator.Name}'."
            );
        }

        var isFirstTopic = simulator.Topics.Count == 0;
        var topic = new AzureEventGridSimulatorTopicResource(name, port, key?.Resource, simulator);
        simulator.AddTopic(topic);

        // The simulator binds each topic's port itself, from its configuration
        builder.WithHttpsEndpoint(port: port, targetPort: port, name: name);

        // The health endpoint answers on every port, so the first topic's is enough
        if (isFirstTopic)
        {
            builder.WithHttpHealthCheck(HealthPath, endpointName: name);
        }

        // Nothing runs a topic itself, so it has to report the simulator's readiness as its own.
        // Without a Running state, WaitFor(topic) would never complete.
        builder.ApplicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(
            simulator,
            (@event, _) =>
                @event
                    .Services.GetRequiredService<ResourceNotificationService>()
                    .PublishUpdateAsync(
                        topic,
                        snapshot => snapshot with { State = KnownResourceStates.Running }
                    )
        );

        return builder
            .ApplicationBuilder.AddResource(topic)
            .WithInitialState(
                new CustomResourceSnapshot
                {
                    ResourceType = "EventGridTopic",
                    State = KnownResourceStates.NotStarted,
                    Properties = [],
                }
            )
            .ExcludeFromManifest();
    }

    /// <summary>
    ///     Delivers the topic's events to a webhook on another resource in the AppHost.
    /// </summary>
    /// <param name="builder">The topic.</param>
    /// <param name="name">The subscriber's name.</param>
    /// <param name="target">The resource receiving the events.</param>
    /// <param name="path">The webhook's path on that resource.</param>
    /// <param name="endpointName">The target's endpoint. Defaults to https, falling back to http.</param>
    /// <param name="disableValidation">
    ///     Whether to skip Event Grid's subscription validation handshake. On by default, because
    ///     the simulator starts validating before the target is ready to answer.
    /// </param>
    /// <returns>The topic's builder.</returns>
    public static IResourceBuilder<AzureEventGridSimulatorTopicResource> WithWebhookSubscriber(
        this IResourceBuilder<AzureEventGridSimulatorTopicResource> builder,
        string name,
        IResourceBuilder<IResourceWithEndpoints> target,
        string path = "/",
        string? endpointName = null,
        bool disableValidation = true
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(path);

        var endpoint = target.GetEndpoint(endpointName ?? GetDefaultEndpointName(target.Resource));
        var relativePath = path.StartsWith('/') ? path : $"/{path}";

        return builder.WithSubscriber(
            "http",
            name,
            ("endpoint", ReferenceExpression.Create($"{endpoint}{relativePath}")),
            ("disableValidation", disableValidation ? "true" : "false")
        );
    }

    /// <summary>
    ///     Delivers the topic's events to an Azure Storage queue, which the simulator creates if needed.
    /// </summary>
    /// <param name="builder">The topic.</param>
    /// <param name="name">The subscriber's name.</param>
    /// <param name="storageQueues">The storage queues resource, such as the result of <c>AddQueues</c>.</param>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>The topic's builder.</returns>
    public static IResourceBuilder<AzureEventGridSimulatorTopicResource> WithStorageQueueSubscriber(
        this IResourceBuilder<AzureEventGridSimulatorTopicResource> builder,
        string name,
        IResourceBuilder<IResourceWithConnectionString> storageQueues,
        string queueName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return builder.WithConnectedSubscriber(
            "storageQueue",
            name,
            storageQueues,
            ("queueName", queueName)
        );
    }

    /// <summary>
    ///     Delivers the topic's events to an Azure Service Bus queue.
    /// </summary>
    /// <param name="builder">The topic.</param>
    /// <param name="name">The subscriber's name.</param>
    /// <param name="serviceBus">The Service Bus namespace resource.</param>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>The topic's builder.</returns>
    public static IResourceBuilder<AzureEventGridSimulatorTopicResource> WithServiceBusQueueSubscriber(
        this IResourceBuilder<AzureEventGridSimulatorTopicResource> builder,
        string name,
        IResourceBuilder<IResourceWithConnectionString> serviceBus,
        string queueName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return builder.WithConnectedSubscriber(
            "serviceBus",
            name,
            serviceBus,
            ("queue", queueName)
        );
    }

    /// <summary>
    ///     Delivers the topic's events to an Azure Service Bus topic.
    /// </summary>
    /// <param name="builder">The topic.</param>
    /// <param name="name">The subscriber's name.</param>
    /// <param name="serviceBus">The Service Bus namespace resource.</param>
    /// <param name="topicName">The name of the Service Bus topic.</param>
    /// <returns>The topic's builder.</returns>
    public static IResourceBuilder<AzureEventGridSimulatorTopicResource> WithServiceBusTopicSubscriber(
        this IResourceBuilder<AzureEventGridSimulatorTopicResource> builder,
        string name,
        IResourceBuilder<IResourceWithConnectionString> serviceBus,
        string topicName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        return builder.WithConnectedSubscriber(
            "serviceBus",
            name,
            serviceBus,
            ("topic", topicName)
        );
    }

    /// <summary>
    ///     Delivers the topic's events to an Azure Event Hub.
    /// </summary>
    /// <param name="builder">The topic.</param>
    /// <param name="name">The subscriber's name.</param>
    /// <param name="eventHubs">The Event Hubs namespace resource.</param>
    /// <param name="eventHubName">The name of the event hub.</param>
    /// <returns>The topic's builder.</returns>
    public static IResourceBuilder<AzureEventGridSimulatorTopicResource> WithEventHubSubscriber(
        this IResourceBuilder<AzureEventGridSimulatorTopicResource> builder,
        string name,
        IResourceBuilder<IResourceWithConnectionString> eventHubs,
        string eventHubName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHubName);

        return builder.WithConnectedSubscriber(
            "eventHub",
            name,
            eventHubs,
            ("eventHubName", eventHubName)
        );
    }

    private static IResourceBuilder<AzureEventGridSimulatorTopicResource> WithConnectedSubscriber(
        this IResourceBuilder<AzureEventGridSimulatorTopicResource> builder,
        string section,
        string name,
        IResourceBuilder<IResourceWithConnectionString> connection,
        (string Setting, object Value) destination
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connection);

        // Delivery fails until the emulator is up, so hold the simulator back until it is
        var simulator = builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Parent);
        simulator.WaitFor(connection);

        return builder.WithSubscriber(
            section,
            name,
            destination,
            ("connectionString", connection.Resource.ConnectionStringExpression)
        );
    }

    private static IResourceBuilder<AzureEventGridSimulatorTopicResource> WithSubscriber(
        this IResourceBuilder<AzureEventGridSimulatorTopicResource> builder,
        string section,
        string name,
        params (string Setting, object Value)[] settings
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var all = settings.ToDictionary(s => s.Setting, s => s.Value, StringComparer.Ordinal);
        all["name"] = name;

        builder.Resource.AddSubscriber(new SimulatorSubscriber(section, all));
        return builder;
    }

    internal static void EnsureHasTopics(AzureEventGridSimulatorResource simulator)
    {
        if (simulator.Topics.Count == 0)
        {
            throw new DistributedApplicationException(
                $"The Event Grid simulator '{simulator.Name}' has no topics. Call AddTopic to add one."
            );
        }
    }

    private static string GetDefaultEndpointName(IResourceWithEndpoints target)
    {
        var endpoints = target.Annotations.OfType<EndpointAnnotation>().ToArray();
        return endpoints.Any(e => string.Equals(e.Name, "https", StringComparison.Ordinal))
            ? "https"
            : "http";
    }

    /// <summary>
    ///     The image and this package are released together, so a released package runs the image
    ///     of its own version. A local build has no such image, and runs the latest one.
    /// </summary>
    private static string GetMatchingImageTag() =>
        ParseImageTag(
            typeof(AzureEventGridSimulatorBuilderExtensions)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
        );

    internal static string ParseImageTag(string? informationalVersion)
    {
        var version = informationalVersion?.Split('+')[0];
        return version is null or UnversionedBuild ? LatestTag : version;
    }
}
