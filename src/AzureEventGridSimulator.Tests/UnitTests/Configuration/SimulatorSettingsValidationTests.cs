using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Configuration;

[Trait("Category", "unit")]
public class SimulatorSettingsValidationTests
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
}
