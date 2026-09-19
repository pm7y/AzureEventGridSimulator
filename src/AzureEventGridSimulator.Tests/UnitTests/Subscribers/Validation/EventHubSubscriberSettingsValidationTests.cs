using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class EventHubSubscriberSettingsValidationTests
{
    private const string SubscriberConnectionString =
        "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123";

    private const string TopicConnectionString =
        "Endpoint=sb://topic-namespace.servicebus.windows.net/;SharedAccessKeyName=TopicKey;SharedAccessKey=topicabc123";

    private static TopicSettings CreateTopic(
        string? eventHubConnectionString = null,
        string? eventHubNamespace = null,
        string? eventHubSharedAccessKeyName = null,
        string? eventHubSharedAccessKey = null
    )
    {
        return new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "TestKey",
            EventHubConnectionString = eventHubConnectionString,
            EventHubNamespace = eventHubNamespace,
            EventHubSharedAccessKeyName = eventHubSharedAccessKeyName,
            EventHubSharedAccessKey = eventHubSharedAccessKey,
        };
    }

    private static TopicSettings CreateTopicWithNamespaceCredentials()
    {
        return CreateTopic(
            eventHubNamespace: "topic-namespace",
            eventHubSharedAccessKeyName: "TopicKey",
            eventHubSharedAccessKey: "topicabc123"
        );
    }

    private static EventHubSubscriberSettings CreateValidConnectionStringSettings()
    {
        return new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
        };
    }

    private static EventHubSubscriberSettings CreateValidNamespaceSettings()
    {
        return new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            Namespace = "my-namespace",
            SharedAccessKeyName = "RootManageSharedAccessKey",
            SharedAccessKey = "abc123",
            EventHubName = "my-event-hub",
        };
    }

    [Fact]
    public void Validate_WithConnectionStringAndEventHubName_ShouldPass()
    {
        var settings = CreateValidConnectionStringSettings();

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithNamespaceCredentialsAndEventHubName_ShouldPass()
    {
        var settings = CreateValidNamespaceSettings();

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithoutName_ShouldThrow()
    {
        var json = """
            {
                "connectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
                "eventHubName": "my-event-hub"
            }
            """;

        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<EventHubSubscriberSettings>(json)
        );
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "   ",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.ParamName.ShouldBe("Name");
    }

    [Fact]
    public void Validate_WithoutEventHubName_ShouldThrow()
    {
        var json = """
            {
                "name": "TestSubscriber",
                "connectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123"
            }
            """;

        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<EventHubSubscriberSettings>(json)
        );
    }

    [Fact]
    public void Validate_WithEmptyEventHubName_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "   ",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("eventHubName");
    }

    [Fact]
    public void Validate_WithoutAuthenticationSettings_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("connectionString");
        exception.Message.ShouldContain("namespace");
    }

    [Fact]
    public void Validate_WithBothConnectionStringAndNamespaceCredentials_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Namespace = "my-namespace",
            SharedAccessKeyName = "RootManageSharedAccessKey",
            SharedAccessKey = "abc123",
            EventHubName = "my-event-hub",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("not both");
    }

    [Fact]
    public void Validate_WithIncompleteNamespaceCredentials_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            Namespace = "my-namespace",
            // Missing SharedAccessKeyName and SharedAccessKey
            EventHubName = "my-event-hub",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("connectionString");
    }

    [Fact]
    public void Validate_WithValidDeliveryProperties_ShouldPass()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "dynamic", Value = "Subject" },
                ["Region"] = new() { Type = "static", Value = "west-us" },
            },
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithInvalidPropertyType_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "invalid", Value = "Subject" },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Label");
        exception.Message.ShouldContain("static");
        exception.Message.ShouldContain("dynamic");
    }

    [Fact]
    public void Validate_WithEmptyPropertyType_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "", Value = "Subject" },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Label");
        exception.Message.ShouldContain("type");
    }

    [Fact]
    public void Validate_WithEmptyPropertyValue_ShouldThrow()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "static", Value = "" },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Label");
        exception.Message.ShouldContain("value");
    }

    [Fact]
    public void EffectiveConnectionString_WithConnectionString_ShouldReturnIt()
    {
        var settings = CreateValidConnectionStringSettings();

        settings.EffectiveConnectionString.ShouldBe(settings.ConnectionString);
    }

    [Fact]
    public void EffectiveConnectionString_WithNamespaceCredentials_ShouldBuildConnectionString()
    {
        var settings = CreateValidNamespaceSettings();

        settings.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123"
        );
    }

    [Fact]
    public void EffectiveConnectionString_WithTopicLevelConnectionString_ShouldFallback()
    {
        var parentTopic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "TestKey",
            EventHubConnectionString =
                "Endpoint=sb://topic-namespace.servicebus.windows.net/;SharedAccessKeyName=TopicKey;SharedAccessKey=topicabc123",
        };

        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            ParentTopic = parentTopic,
        };

        settings.EffectiveConnectionString.ShouldBe(parentTopic.EventHubConnectionString);
    }

    [Fact]
    public void EffectiveConnectionString_WithTopicLevelNamespaceCredentials_ShouldFallback()
    {
        var parentTopic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "TestKey",
            EventHubNamespace = "topic-namespace",
            EventHubSharedAccessKeyName = "TopicKey",
            EventHubSharedAccessKey = "topicabc123",
        };

        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            ParentTopic = parentTopic,
        };

        settings.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://topic-namespace.servicebus.windows.net/;SharedAccessKeyName=TopicKey;SharedAccessKey=topicabc123"
        );
    }

    [Fact]
    public void SubscriberType_ShouldReturnEventHub()
    {
        var settings = CreateValidConnectionStringSettings();

        settings.SubscriberType.ShouldBe("eventHub");
    }

    [Fact]
    public void Validate_WithFilter_ShouldValidateFilter()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            Filter = new FilterSetting
            {
                IncludedEventTypes = new List<string> { "MyEvent" },
                SubjectBeginsWith = "test/",
            },
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithTopicLevelConnectionString_ShouldPass()
    {
        var parentTopic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "TestKey",
            EventHubConnectionString =
                "Endpoint=sb://topic-namespace.servicebus.windows.net/;SharedAccessKeyName=TopicKey;SharedAccessKey=topicabc123",
        };

        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            ParentTopic = parentTopic,
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenOnlyTopicLevelNamespaceCredentials_WhenValidated_ThenNoException()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            ParentTopic = CreateTopicWithNamespaceCredentials(),
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Theory]
    [InlineData(null, "TopicKey", "topicabc123")]
    [InlineData("topic-namespace", null, "topicabc123")]
    [InlineData("topic-namespace", "TopicKey", null)]
    public void GivenIncompleteTopicLevelNamespaceCredentials_WhenValidated_ThenThrowsAndHasNoEffectiveConnectionString(
        string? eventHubNamespace,
        string? sharedAccessKeyName,
        string? sharedAccessKey
    )
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            ParentTopic = CreateTopic(
                eventHubNamespace: eventHubNamespace,
                eventHubSharedAccessKeyName: sharedAccessKeyName,
                eventHubSharedAccessKey: sharedAccessKey
            ),
        };

        settings.EffectiveConnectionString.ShouldBeNull();
        Should.Throw<ArgumentException>(() => settings.Validate());
    }

    [Fact]
    public void GivenSubscriberAndTopicLevelConnectionStrings_WhenReadingEffectiveConnectionString_ThenSubscriberLevelWins()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            ParentTopic = CreateTopic(eventHubConnectionString: TopicConnectionString),
        };

        settings.EffectiveConnectionString.ShouldBe(SubscriberConnectionString);
    }

    [Fact]
    public void GivenSubscriberNamespaceCredentialsAndTopicLevelConnectionString_WhenReadingEffectiveConnectionString_ThenSubscriberLevelWins()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            Namespace = "sub-namespace",
            SharedAccessKeyName = "SubKey",
            SharedAccessKey = "subabc123",
            EventHubName = "my-event-hub",
            ParentTopic = CreateTopic(eventHubConnectionString: TopicConnectionString),
        };

        settings.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://sub-namespace.servicebus.windows.net/;SharedAccessKeyName=SubKey;SharedAccessKey=subabc123"
        );
    }

    [Fact]
    public void GivenTopicLevelConnectionStringAndNamespaceCredentials_WhenReadingEffectiveConnectionString_ThenTopicConnectionStringWins()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            ParentTopic = CreateTopic(
                eventHubConnectionString: TopicConnectionString,
                eventHubNamespace: "other-namespace",
                eventHubSharedAccessKeyName: "OtherKey",
                eventHubSharedAccessKey: "otherabc123"
            ),
        };

        settings.EffectiveConnectionString.ShouldBe(TopicConnectionString);
    }

    [Fact]
    public void GivenOnlyServiceBusCredentialsOnTopic_WhenValidated_ThenThrowsAndHasNoEffectiveConnectionString()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            ParentTopic = new TopicSettings
            {
                Name = "test-topic",
                Port = 60101,
                ServiceBusConnectionString = TopicConnectionString,
                ServiceBusNamespace = "topic-namespace",
                ServiceBusSharedAccessKeyName = "TopicKey",
                ServiceBusSharedAccessKey = "topicabc123",
            },
        };

        settings.EffectiveConnectionString.ShouldBeNull();
        Should.Throw<ArgumentException>(() => settings.Validate());
    }

    [Fact]
    public void GivenInvalidRetryPolicy_WhenValidated_ThenThrowsArgumentException()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "MaxDeliveryAttempts must be between 1 and 30. (Parameter 'MaxDeliveryAttempts')"
        );
    }

    // Validation order for Event Hub: name, eventHubName, authentication, properties, then
    // filter, retry policy and dead-letter. Each test below breaks two adjacent rules and
    // pins which message wins, word for word.

    [Fact]
    public void GivenBlankNameAndEverythingElseInvalid_WhenValidated_ThenNameErrorIsReported()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "   ",
            EventHubName = "   ",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "", Value = "Subject" },
            },
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe("Subscriber name is required. (Parameter 'Name')");
    }

    [Fact]
    public void GivenBlankEventHubNameAndNoCredentials_WhenValidated_ThenEventHubNameErrorIsReported()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "   ",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Event Hub subscriber 'TestSubscriber' must specify an eventHubName."
        );
    }

    [Fact]
    public void GivenConnectionStringAndNamespaceCredentialsAndInvalidProperty_WhenValidated_ThenNotBothErrorIsReported()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Namespace = "my-namespace",
            SharedAccessKeyName = "RootManageSharedAccessKey",
            SharedAccessKey = "abc123",
            EventHubName = "my-event-hub",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "", Value = "Subject" },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Event Hub subscriber 'TestSubscriber' should specify either connectionString or namespace credentials, not both."
        );
    }

    [Fact]
    public void GivenConnectionStringAndOnlyPartOfNamespaceCredentials_WhenValidated_ThenNotBothErrorIsReported()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            SharedAccessKey = "abc123",
            EventHubName = "my-event-hub",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Event Hub subscriber 'TestSubscriber' should specify either connectionString or namespace credentials, not both."
        );
    }

    [Fact]
    public void GivenNoCredentialsAndInvalidProperty_WhenValidated_ThenAuthenticationErrorIsReported()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            EventHubName = "my-event-hub",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "", Value = "Subject" },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Event Hub subscriber 'TestSubscriber' must have either a connectionString or namespace + sharedAccessKeyName + sharedAccessKey, either at subscriber or topic level."
        );
    }

    [Fact]
    public void GivenInvalidPropertyAndInvalidFilter_WhenValidated_ThenPropertyErrorIsReported()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "", Value = "Subject" },
            },
            Filter = new FilterSetting { AdvancedFilters = [new AdvancedFilterSetting()] },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe("Property 'Label' must have a type. (Parameter 'Type')");
    }

    [Fact]
    public void GivenInvalidFilterAndInvalidRetryPolicy_WhenValidated_ThenFilterErrorIsReported()
    {
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            Filter = new FilterSetting { AdvancedFilters = [new AdvancedFilterSetting()] },
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe("A filter key must be provided (Parameter 'Key')");
    }

    [Fact]
    public void GivenDeadLetterWithBlankFolderPath_WhenValidated_ThenDefaultFolderPathIsApplied()
    {
        var deadLetter = new DeadLetterSettings { FolderPath = "" };
        var settings = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            EventHubName = "my-event-hub",
            DeadLetter = deadLetter,
        };

        settings.Validate();

        deadLetter.FolderPath.ShouldBe("./dead-letters");
    }
}
