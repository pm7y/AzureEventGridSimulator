using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Configuration;

[Trait("Category", "unit")]
public class SimulatorSettingsValidationTests : SubscribersSettingsTestBase
{
    private static HttpSubscriberSettings CreateSubscriber(string name)
    {
        return new HttpSubscriberSettings
        {
            Name = name,
            Endpoint = "https://localhost:5000/webhook",
        };
    }

    private static TopicSettings CreateTopic(
        string name,
        int port,
        params HttpSubscriberSettings[] subscribers
    )
    {
        return new TopicSettings
        {
            Name = name,
            Port = port,
            Key = "TheLocal+DevelopmentKey=",
            Subscribers = new SubscribersSettings { Http = subscribers },
        };
    }

    private static TopicSettings CreateTopic(string name, int port, SubscribersSettings subscribers)
    {
        return new TopicSettings
        {
            Name = name,
            Port = port,
            Key = "TheLocal+DevelopmentKey=",
            Subscribers = subscribers,
        };
    }

    [Fact]
    public void GivenSameSubscriberNameOnDifferentTopics_WhenValidated_ThenSucceeds()
    {
        // Subscriber name uniqueness is scoped per topic (matching Azure), so the
        // same name may be reused across topics.
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic("topic-one", 60101, CreateSubscriber("SharedName")),
                CreateTopic("topic-two", 60102, CreateSubscriber("SharedName")),
            ],
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenDuplicateSubscriberNamesOnSameTopic_WhenValidated_ThenThrows()
    {
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic(
                    "topic-one",
                    60101,
                    CreateSubscriber("DuplicateName"),
                    CreateSubscriber("DuplicateName")
                ),
            ],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldContain("topic-one");
        exception.Message.ShouldContain("DuplicateName");
    }

    [Fact]
    public void GivenSubscriberNamesDifferingOnlyByCase_WhenValidated_ThenThrows()
    {
        // Name comparison is case-insensitive, matching Azure
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic(
                    "topic-one",
                    60101,
                    CreateSubscriber("My-Subscriber"),
                    CreateSubscriber("my-subscriber")
                ),
            ],
        };

        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void GivenMultipleDuplicateSubscriberNames_WhenValidated_ThenAllDuplicatesListed()
    {
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic(
                    "topic-one",
                    60101,
                    CreateSubscriber("Dup1"),
                    CreateSubscriber("Dup1"),
                    CreateSubscriber("Dup2"),
                    CreateSubscriber("Dup2")
                ),
            ],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldBe(
            "Each subscriber on a topic must have a unique name. Duplicate name(s) on topic 'topic-one': Dup1, Dup2."
        );
    }

    [Fact]
    public void GivenDuplicateTopicPorts_WhenValidated_ThenThrows()
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic-one", 60101), CreateTopic("topic-two", 60101)],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldContain("unique port");
    }

    [Fact]
    public void GivenDuplicateTopicNames_WhenValidated_ThenThrows()
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("same-topic", 60101), CreateTopic("same-topic", 60102)],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldContain("unique name");
    }

    [Fact]
    public void GivenUniqueSubscriberNamesAcrossAllTypes_WhenValidated_ThenSucceeds()
    {
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic(
                    "topic-one",
                    60101,
                    new SubscribersSettings
                    {
                        Http =
                        [
                            CreateValidHttpSubscriber("Http1"),
                            CreateValidHttpSubscriber("Http2"),
                        ],
                        ServiceBus =
                        [
                            CreateValidServiceBusSubscriber("ServiceBus1"),
                            CreateValidServiceBusSubscriber("ServiceBus2"),
                        ],
                        StorageQueue =
                        [
                            CreateValidStorageQueueSubscriber("StorageQueue1"),
                            CreateValidStorageQueueSubscriber("StorageQueue2"),
                        ],
                        EventHub =
                        [
                            CreateValidEventHubSubscriber("EventHub1"),
                            CreateValidEventHubSubscriber("EventHub2"),
                        ],
                    }
                ),
            ],
        };

        Should.NotThrow(() => settings.Validate());
        settings.Topics[0].Subscribers.All.Count().ShouldBe(8);
    }

    [Fact]
    public void GivenDuplicateNamesAcrossSubscriberTypes_WhenValidated_ThenThrows()
    {
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic(
                    "topic-one",
                    60101,
                    new SubscribersSettings
                    {
                        Http = [CreateValidHttpSubscriber("SharedName")],
                        EventHub = [CreateValidEventHubSubscriber("SharedName")],
                    }
                ),
            ],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldBe(
            "Each subscriber on a topic must have a unique name. Duplicate name(s) on topic 'topic-one': SharedName."
        );
    }

    [Fact]
    public void GivenEventHubSubscriberWithoutCredentials_WhenValidated_ThenThrows()
    {
        // Subscriber-level errors propagate unwrapped, so this is an ArgumentException
        // rather than the InvalidOperationException used by the cross-subscriber rules.
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic(
                    "topic-one",
                    60101,
                    new SubscribersSettings
                    {
                        EventHub =
                        [
                            new EventHubSubscriberSettings
                            {
                                Name = "EventHub1",
                                EventHubName = "test-hub",
                            },
                        ],
                    }
                ),
            ],
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());

        exception.Message.ShouldContain(
            "Event Hub subscriber 'EventHub1' must have either a connectionString"
        );
    }

    [Fact]
    public void GivenTopicWithNoSubscribers_WhenValidated_ThenSucceeds()
    {
        var settings = new SimulatorSettings { Topics = [CreateTopic("topic-one", 60101)] };

        Should.NotThrow(() => settings.Validate());
    }

    [Theory]
    [InlineData("topic_one")]
    [InlineData("topic one")]
    [InlineData("topic.one")]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenTopicNameWithInvalidCharacters_WhenValidated_ThenThrows(string topicName)
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic(topicName, 60101, CreateSubscriber("subscriber-one"))],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldBe("A topic name can only contain letters, numbers, and dashes.");
    }

    [Theory]
    [InlineData("subscriber_one")]
    [InlineData("subscriber one")]
    [InlineData("subscriber.one")]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenSubscriberNameWithInvalidCharacters_WhenValidated_ThenThrows(
        string subscriberName
    )
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic-one", 60101, CreateSubscriber(subscriberName))],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldBe(
            "A subscriber name can only contain letters, numbers, and dashes."
        );
    }

    [Fact]
    public void GivenValidTopicAndSubscriberNames_WhenValidated_ThenSucceeds()
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("Topic-1", 60101, CreateSubscriber("Subscriber-2"))],
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenInvalidTopicAndSubscriberNames_WhenValidated_ThenTopicNameIsReportedFirst()
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic_one", 60101, CreateSubscriber("subscriber_one"))],
        };

        var exception = Should.Throw<InvalidOperationException>(() => settings.Validate());

        exception.Message.ShouldBe("A topic name can only contain letters, numbers, and dashes.");
    }

    [Fact]
    public void GivenBrokerSubscribersWithoutOwnCredentials_WhenValidated_ThenTheyInheritTopicCredentials()
    {
        // Parent-topic wiring has to happen before the subscribers validate their credentials.
        const string serviceBusConnectionString =
            "Endpoint=sb://topic-sb.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123";
        const string storageQueueConnectionString =
            "DefaultEndpointsProtocol=https;AccountName=topic;AccountKey=abc123;EndpointSuffix=core.windows.net";
        const string eventHubConnectionString =
            "Endpoint=sb://topic-eh.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123";

        var serviceBus = new ServiceBusSubscriberSettings { Name = "ServiceBus1", Queue = "q" };
        var storageQueue = new StorageQueueSubscriberSettings
        {
            Name = "StorageQueue1",
            QueueName = "q",
        };
        var eventHub = new EventHubSubscriberSettings { Name = "EventHub1", EventHubName = "hub" };

        var settings = new SimulatorSettings
        {
            Topics =
            [
                new TopicSettings
                {
                    Name = "topic-one",
                    Port = 60101,
                    ServiceBusConnectionString = serviceBusConnectionString,
                    StorageQueueConnectionString = storageQueueConnectionString,
                    EventHubConnectionString = eventHubConnectionString,
                    Subscribers = new SubscribersSettings
                    {
                        ServiceBus = [serviceBus],
                        StorageQueue = [storageQueue],
                        EventHub = [eventHub],
                    },
                },
            ],
        };

        Should.NotThrow(() => settings.Validate());
        serviceBus.EffectiveConnectionString.ShouldBe(serviceBusConnectionString);
        storageQueue.EffectiveConnectionString.ShouldBe(storageQueueConnectionString);
        eventHub.EffectiveConnectionString.ShouldBe(eventHubConnectionString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenDeadLetterWithBlankFolderPath_WhenValidated_ThenDefaultFolderPathIsApplied(
        string? folderPath
    )
    {
        var deadLetter = new DeadLetterSettings { FolderPath = folderPath! };
        var subscriber = new HttpSubscriberSettings
        {
            Name = "subscriber-one",
            Endpoint = "https://localhost:5000/webhook",
            DeadLetter = deadLetter,
        };
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic-one", 60101, subscriber)],
        };

        settings.Validate();

        deadLetter.FolderPath.ShouldBe("./dead-letters");
    }
}
