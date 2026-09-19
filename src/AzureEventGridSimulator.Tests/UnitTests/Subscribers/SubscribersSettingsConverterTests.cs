using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

[Trait("Category", "unit")]
public class SubscribersSettingsConverterTests
{
    private static readonly string[] _expected = ["Http1", "Http2", "SB1"];

    [Fact]
    public void LegacyArrayFormat_ShouldDeserializeAsHttpSubscribers()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": [
                        {
                            "name": "LegacySubscriber",
                            "endpoint": "https://example.com/webhook"
                        }
                    ]
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.ShouldNotBeNull();
        settings.Topics.ShouldHaveSingleItem();

        var topic = settings.Topics.First();
        topic.Subscribers.ShouldNotBeNull();
        topic.Subscribers.HttpSubscribers.ShouldHaveSingleItem();
        topic.Subscribers.ServiceBusSubscribers.ShouldBeEmpty();

        var httpSub = topic.Subscribers.HttpSubscribers.First();
        httpSub.Name.ShouldBe("LegacySubscriber");
        httpSub.Endpoint.ShouldBe("https://example.com/webhook");
    }

    [Fact]
    public void LegacyArrayFormat_WithMultipleSubscribers_ShouldDeserializeAll()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": [
                        {
                            "name": "Subscriber1",
                            "endpoint": "https://example.com/webhook1"
                        },
                        {
                            "name": "Subscriber2",
                            "endpoint": "https://example.com/webhook2"
                        }
                    ]
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings
            .ShouldNotBeNullAnd()
            .Topics.First()
            .Subscribers.HttpSubscribers.Count()
            .ShouldBe(2);
    }

    [Fact]
    public void NewGroupedFormat_WithOnlyHttpSubscribers_ShouldDeserialize()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": {
                        "http": [
                            {
                                "name": "HttpSubscriber",
                                "endpoint": "https://example.com/webhook"
                            }
                        ]
                    }
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.ShouldNotBeNull();
        var topic = settings.Topics.First();
        topic.Subscribers.HttpSubscribers.ShouldHaveSingleItem();
        topic.Subscribers.ServiceBusSubscribers.ShouldBeEmpty();

        var httpSub = topic.Subscribers.HttpSubscribers.First();
        httpSub.Name.ShouldBe("HttpSubscriber");
    }

    [Fact]
    public void NewGroupedFormat_WithOnlyServiceBusSubscribers_ShouldDeserialize()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": {
                        "serviceBus": [
                            {
                                "name": "ServiceBusSubscriber",
                                "connectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
                                "queue": "my-queue"
                            }
                        ]
                    }
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.ShouldNotBeNull();
        var topic = settings.Topics.First();
        topic.Subscribers.HttpSubscribers.ShouldBeEmpty();
        topic.Subscribers.ServiceBusSubscribers.ShouldHaveSingleItem();

        var sbSub = topic.Subscribers.ServiceBusSubscribers.First();
        sbSub.Name.ShouldBe("ServiceBusSubscriber");
        sbSub.Queue.ShouldBe("my-queue");
    }

    [Fact]
    public void NewGroupedFormat_WithBothSubscriberTypes_ShouldDeserialize()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": {
                        "http": [
                            {
                                "name": "HttpSubscriber",
                                "endpoint": "https://example.com/webhook"
                            }
                        ],
                        "serviceBus": [
                            {
                                "name": "ServiceBusSubscriber",
                                "connectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
                                "topic": "my-topic"
                            }
                        ]
                    }
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.ShouldNotBeNull();
        var topic = settings.Topics.First();
        topic.Subscribers.HttpSubscribers.ShouldHaveSingleItem();
        topic.Subscribers.ServiceBusSubscribers.ShouldHaveSingleItem();
    }

    [Fact]
    public void ServiceBusSubscriber_WithNamespaceCredentials_ShouldDeserialize()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": {
                        "serviceBus": [
                            {
                                "name": "ServiceBusSubscriber",
                                "namespace": "my-namespace",
                                "sharedAccessKeyName": "RootManageSharedAccessKey",
                                "sharedAccessKey": "abc123",
                                "queue": "my-queue"
                            }
                        ]
                    }
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var sbSub = settings
            .ShouldNotBeNullAnd()
            .Topics.First()
            .Subscribers.ServiceBusSubscribers.First();
        sbSub.Namespace.ShouldBe("my-namespace");
        sbSub.SharedAccessKeyName.ShouldBe("RootManageSharedAccessKey");
        sbSub.SharedAccessKey.ShouldBe("abc123");
        sbSub.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123"
        );
    }

    [Fact]
    public void ServiceBusSubscriber_WithDeliveryProperties_ShouldDeserialize()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": {
                        "serviceBus": [
                            {
                                "name": "ServiceBusSubscriber",
                                "connectionString": "Endpoint=sb://ns.servicebus.windows.net/",
                                "queue": "my-queue",
                                "properties": {
                                    "Label": { "type": "dynamic", "value": "Subject" },
                                    "Region": { "type": "static", "value": "west-us" }
                                }
                            }
                        ]
                    }
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var sbSub = settings
            .ShouldNotBeNullAnd()
            .Topics.First()
            .Subscribers.ServiceBusSubscribers.First();
        sbSub.Properties.ShouldNotBeNull();
        sbSub.Properties.Count.ShouldBe(2);

        sbSub.Properties["Label"].IsDynamic.ShouldBeTrue();
        sbSub.Properties["Label"].Value.ShouldBe("Subject");

        sbSub.Properties["Region"].IsStatic.ShouldBeTrue();
        sbSub.Properties["Region"].Value.ShouldBe("west-us");
    }

    [Fact]
    public void EmptySubscribersArray_ShouldDeserializeToEmptyLists()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": []
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var validSettings = settings.ShouldNotBeNullAnd();
        validSettings.Topics.First().Subscribers.HttpSubscribers.ShouldBeEmpty();
        validSettings.Topics.First().Subscribers.ServiceBusSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void EmptySubscribersObject_ShouldDeserializeToEmptyLists()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": {}
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var validSettings = settings.ShouldNotBeNullAnd();
        validSettings.Topics.First().Subscribers.HttpSubscribers.ShouldBeEmpty();
        validSettings.Topics.First().Subscribers.ServiceBusSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void AllSubscribers_ShouldReturnCombinedList()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": {
                        "http": [
                            { "name": "Http1", "endpoint": "https://a.com" },
                            { "name": "Http2", "endpoint": "https://b.com" }
                        ],
                        "serviceBus": [
                            { "name": "SB1", "connectionString": "Endpoint=sb://ns.servicebus.windows.net/", "queue": "q1" }
                        ]
                    }
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var allSubscribers = settings.ShouldNotBeNullAnd().Topics.First().Subscribers.All.ToList();
        allSubscribers.Count.ShouldBe(3);
        allSubscribers.Select(s => s.Name).ShouldBe(_expected);
    }

    [Fact]
    public void Serialization_ShouldWriteInNewFormat()
    {
        var settings = new SubscribersSettings
        {
            Http =
            [
                new HttpSubscriberSettings { Name = "HttpSub", Endpoint = "https://example.com" },
            ],
            ServiceBus =
            [
                new ServiceBusSubscriberSettings
                {
                    Name = "SBSub",
                    ConnectionString = "Endpoint=sb://ns.servicebus.windows.net/",
                    Queue = "my-queue",
                },
            ],
        };

        var json = JsonSerializer.Serialize(settings);

        json.ShouldContain("\"http\":");
        json.ShouldContain("\"serviceBus\":");
        json.ShouldContain("\"HttpSub\"");
        json.ShouldContain("\"SBSub\"");
    }

    [Fact]
    public void LegacyFormat_WithFilter_ShouldDeserialize()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": [
                        {
                            "name": "FilteredSubscriber",
                            "endpoint": "https://example.com/webhook",
                            "filter": {
                                "includedEventTypes": ["MyEvent"],
                                "subjectBeginsWith": "test/",
                                "subjectEndsWith": null,
                                "isSubjectCaseSensitive": false,
                                "advancedFilters": null
                            }
                        }
                    ]
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var httpSub = settings
            .ShouldNotBeNullAnd()
            .Topics.First()
            .Subscribers.HttpSubscribers.First();
        httpSub.Filter.ShouldNotBeNull();
        httpSub.Filter.IncludedEventTypes.ShouldNotBeNullAnd().ShouldContain("MyEvent");
        httpSub.Filter.SubjectBeginsWith.ShouldBe("test/");
    }

    [Fact]
    public void InvalidTokenType_ShouldThrowJsonException()
    {
        const string json = """

            {
                "topics": [{
                    "name": "MyTopic",
                    "port": 60101,
                    "key": "TheKey=",
                    "subscribers": "invalid_string"
                }]
            }
            """;

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SimulatorSettings>(json));
    }

    // The tests below pin the config shape of each subscriber type: the JSON property names it
    // reads and writes, and that the properties every type shares bind through both loading
    // paths (System.Text.Json and ConfigurationBinder).

    private const string SharedSubscriberPropertiesJson = """
        {
            "topics": [{
                "name": "SharedTopic",
                "port": 60101,
                "subscribers": {
                    "http": [{
                        "name": "HttpSubscriber",
                        "endpoint": "https://example.com/webhook",
                        "disabled": true,
                        "deliverySchema": "CloudEventV1_0",
                        "filter": { "includedEventTypes": ["Order.Created"] },
                        "retryPolicy": { "maxDeliveryAttempts": 5, "eventTimeToLiveInMinutes": 60 },
                        "deadLetter": { "folderPath": "./dl" }
                    }],
                    "serviceBus": [{
                        "name": "ServiceBusSubscriber",
                        "namespace": "sb-ns",
                        "sharedAccessKeyName": "SbKey",
                        "sharedAccessKey": "sbsecret",
                        "topic": "orders",
                        "properties": { "Source": { "type": "static", "value": "simulator" } },
                        "disabled": true,
                        "deliverySchema": "CloudEventV1_0",
                        "filter": { "includedEventTypes": ["Order.Created"] },
                        "retryPolicy": { "maxDeliveryAttempts": 5, "eventTimeToLiveInMinutes": 60 },
                        "deadLetter": { "folderPath": "./dl" }
                    }],
                    "storageQueue": [{
                        "name": "StorageQueueSubscriber",
                        "connectionString": "UseDevelopmentStorage=true",
                        "queueName": "orders",
                        "disabled": true,
                        "deliverySchema": "CloudEventV1_0",
                        "filter": { "includedEventTypes": ["Order.Created"] },
                        "retryPolicy": { "maxDeliveryAttempts": 5, "eventTimeToLiveInMinutes": 60 },
                        "deadLetter": { "folderPath": "./dl" }
                    }],
                    "eventHub": [{
                        "name": "EventHubSubscriber",
                        "namespace": "eh-ns",
                        "sharedAccessKeyName": "EhKey",
                        "sharedAccessKey": "ehsecret",
                        "eventHubName": "orders-hub",
                        "properties": { "Source": { "type": "static", "value": "simulator" } },
                        "disabled": true,
                        "deliverySchema": "CloudEventV1_0",
                        "filter": { "includedEventTypes": ["Order.Created"] },
                        "retryPolicy": { "maxDeliveryAttempts": 5, "eventTimeToLiveInMinutes": 60 },
                        "deadLetter": { "folderPath": "./dl" }
                    }]
                }
            }]
        }
        """;

    [Fact]
    public void GivenSharedPropertiesOnEverySubscriberType_WhenDeserialized_ThenEveryTypeBindsThem()
    {
        var settings = JsonSerializer
            .Deserialize<SimulatorSettings>(SharedSubscriberPropertiesJson)
            .ShouldNotBeNullAnd();
        settings.Validate();

        ShouldHaveSharedPropertiesOnEveryType(settings);
    }

    [Fact]
    public void GivenSharedPropertiesOnEverySubscriberType_WhenBoundFromConfiguration_ThenEveryTypeBindsThem()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SharedSubscriberPropertiesJson));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        using var serviceProvider = new ServiceCollection()
            .AddSimulatorSettings(configuration)
            .BuildServiceProvider();

        ShouldHaveSharedPropertiesOnEveryType(
            serviceProvider.GetRequiredService<SimulatorSettings>()
        );
    }

    [Fact]
    public void GivenEachSubscriberType_WhenSerialized_ThenOnlyItsConfigPropertiesAreWritten()
    {
        string[] shared =
        [
            "name",
            "filter",
            "disabled",
            "deliverySchema",
            "retryPolicy",
            "deadLetter",
        ];
        string[] namespaceCredentials =
        [
            "connectionString",
            "namespace",
            "sharedAccessKeyName",
            "sharedAccessKey",
            "properties",
        ];

        SerializedPropertyNames(
                new HttpSubscriberSettings { Name = "Http", Endpoint = "https://example.com" }
            )
            .ShouldBe([.. shared, "endpoint", "disableValidation"], ignoreOrder: true);
        SerializedPropertyNames(
                new ServiceBusSubscriberSettings
                {
                    Name = "ServiceBus",
                    ConnectionString = "Endpoint=sb://ns.servicebus.windows.net/",
                    Queue = "my-queue",
                    ParentTopic = new TopicSettings { Name = "topic", Port = 60101 },
                }
            )
            .ShouldBe([.. shared, .. namespaceCredentials, "topic", "queue"], ignoreOrder: true);
        SerializedPropertyNames(
                new StorageQueueSubscriberSettings
                {
                    Name = "StorageQueue",
                    ConnectionString = "UseDevelopmentStorage=true",
                    QueueName = "my-queue",
                    ParentTopic = new TopicSettings { Name = "topic", Port = 60101 },
                }
            )
            .ShouldBe([.. shared, "connectionString", "queueName"], ignoreOrder: true);
        SerializedPropertyNames(
                new EventHubSubscriberSettings
                {
                    Name = "EventHub",
                    ConnectionString = "Endpoint=sb://ns.servicebus.windows.net/",
                    EventHubName = "my-hub",
                    ParentTopic = new TopicSettings { Name = "topic", Port = 60101 },
                }
            )
            .ShouldBe([.. shared, .. namespaceCredentials, "eventHubName"], ignoreOrder: true);
    }

    private static string[] SerializedPropertyNames(ISubscriberSettings subscriber)
    {
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(subscriber, subscriber.GetType())
        );

        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }

    private static void ShouldHaveSharedPropertiesOnEveryType(SimulatorSettings settings)
    {
        var subscribers = settings.Topics.ShouldHaveSingleItem().Subscribers;
        subscribers
            .All.Select(s => s.SubscriberType)
            .ShouldBe(["http", "serviceBus", "storageQueue", "eventHub"]);

        foreach (var subscriber in subscribers.All)
        {
            subscriber.Disabled.ShouldBeTrue(subscriber.Name);
            subscriber.DeliverySchema.ShouldBe(EventSchema.CloudEventV1_0, subscriber.Name);
            subscriber
                .Filter.ShouldNotBeNullAnd(subscriber.Name)
                .IncludedEventTypes.ShouldBe(["Order.Created"], subscriber.Name);
            var retryPolicy = subscriber.RetryPolicy.ShouldNotBeNullAnd(subscriber.Name);
            retryPolicy.MaxDeliveryAttempts.ShouldBe(5, subscriber.Name);
            retryPolicy.EventTimeToLiveInMinutes.ShouldBe(60, subscriber.Name);
            subscriber
                .DeadLetter.ShouldNotBeNullAnd(subscriber.Name)
                .FolderPath.ShouldBe("./dl", subscriber.Name);
        }

        var serviceBus = subscribers.ServiceBusSubscribers.Single();
        serviceBus.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://sb-ns.servicebus.windows.net/;SharedAccessKeyName=SbKey;SharedAccessKey=sbsecret"
        );
        serviceBus.Properties.ShouldNotBeNullAnd()["Source"].Value.ShouldBe("simulator");

        var eventHub = subscribers.EventHubSubscribers.Single();
        eventHub.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://eh-ns.servicebus.windows.net/;SharedAccessKeyName=EhKey;SharedAccessKey=ehsecret"
        );
        eventHub.Properties.ShouldNotBeNullAnd()["Source"].Value.ShouldBe("simulator");
    }
}
