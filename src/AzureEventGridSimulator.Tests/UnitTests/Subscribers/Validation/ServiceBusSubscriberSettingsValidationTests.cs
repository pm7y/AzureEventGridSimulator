using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class ServiceBusSubscriberSettingsValidationTests
{
    private const string SubscriberConnectionString =
        "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123";

    private const string TopicConnectionString =
        "Endpoint=sb://topic-namespace.servicebus.windows.net/;SharedAccessKeyName=TopicKey;SharedAccessKey=topicabc123";

    private static TopicSettings CreateTopic(
        string? serviceBusConnectionString = null,
        string? serviceBusNamespace = null,
        string? serviceBusSharedAccessKeyName = null,
        string? serviceBusSharedAccessKey = null
    )
    {
        return new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "TestKey",
            ServiceBusConnectionString = serviceBusConnectionString,
            ServiceBusNamespace = serviceBusNamespace,
            ServiceBusSharedAccessKeyName = serviceBusSharedAccessKeyName,
            ServiceBusSharedAccessKey = serviceBusSharedAccessKey,
        };
    }

    private static TopicSettings CreateTopicWithNamespaceCredentials()
    {
        return CreateTopic(
            serviceBusNamespace: "topic-namespace",
            serviceBusSharedAccessKeyName: "TopicKey",
            serviceBusSharedAccessKey: "topicabc123"
        );
    }

    private static ServiceBusSubscriberSettings CreateValidConnectionStringSettings(
        string? queue = "my-queue",
        string? topic = null
    )
    {
        return new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = queue,
            Topic = topic,
        };
    }

    private static ServiceBusSubscriberSettings CreateValidNamespaceSettings(
        string? queue = "my-queue",
        string? topic = null
    )
    {
        return new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Namespace = "my-namespace",
            SharedAccessKeyName = "RootManageSharedAccessKey",
            SharedAccessKey = "abc123",
            Queue = queue,
            Topic = topic,
        };
    }

    [Fact]
    public void Validate_WithConnectionStringAndQueue_ShouldPass()
    {
        var settings = CreateValidConnectionStringSettings();

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithConnectionStringAndTopic_ShouldPass()
    {
        var settings = CreateValidConnectionStringSettings(null, "my-topic");

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithNamespaceCredentialsAndQueue_ShouldPass()
    {
        var settings = CreateValidNamespaceSettings();

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithNamespaceCredentialsAndTopic_ShouldPass()
    {
        var settings = CreateValidNamespaceSettings(null, "my-topic");

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithoutName_ShouldThrow()
    {
        var json = """
            {
                "connectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
                "queue": "my-queue"
            }
            """;

        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<ServiceBusSubscriberSettings>(json)
        );
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldThrow()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "   ",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.ParamName.ShouldBe("Name");
    }

    [Fact]
    public void Validate_WithoutAuthenticationSettings_ShouldThrow()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "queue",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("connectionString");
        exception.Message.ShouldContain("namespace");
    }

    [Fact]
    public void Validate_WithBothConnectionStringAndNamespaceCredentials_ShouldThrow()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Namespace = "my-namespace",
            SharedAccessKeyName = "RootManageSharedAccessKey",
            SharedAccessKey = "abc123",
            Queue = "my-queue",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("not both");
    }

    [Fact]
    public void Validate_WithIncompleteNamespaceCredentials_ShouldThrow()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Namespace = "my-namespace",
            // Missing SharedAccessKeyName and SharedAccessKey
            Queue = "my-queue",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("connectionString");
    }

    [Fact]
    public void Validate_WithoutDestination_ShouldThrow()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = null,
            Topic = null,
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("topic or queue");
    }

    [Fact]
    public void Validate_WithBothQueueAndTopic_ShouldThrow()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
            Topic = "my-topic",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("not both");
    }

    [Fact]
    public void Validate_WithValidDeliveryProperties_ShouldPass()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
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
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
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
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
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
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
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
    public void IsTopic_WithTopicSet_ShouldReturnTrue()
    {
        var settings = CreateValidConnectionStringSettings(null, "my-topic");

        settings.IsTopic.ShouldBeTrue();
    }

    [Fact]
    public void IsTopic_WithQueueSet_ShouldReturnFalse()
    {
        var settings = CreateValidConnectionStringSettings();

        settings.IsTopic.ShouldBeFalse();
    }

    [Fact]
    public void DestinationName_WithTopic_ShouldReturnTopicName()
    {
        var settings = CreateValidConnectionStringSettings(null, "my-topic");

        settings.DestinationName.ShouldBe("my-topic");
    }

    [Fact]
    public void DestinationName_WithQueue_ShouldReturnQueueName()
    {
        var settings = CreateValidConnectionStringSettings();

        settings.DestinationName.ShouldBe("my-queue");
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
    public void SubscriberType_ShouldReturnServiceBus()
    {
        var settings = CreateValidConnectionStringSettings();

        settings.SubscriberType.ShouldBe("serviceBus");
    }

    [Fact]
    public void Validate_WithFilter_ShouldValidateFilter()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
            Filter = new FilterSetting
            {
                IncludedEventTypes = new List<string> { "MyEvent" },
                SubjectBeginsWith = "test/",
            },
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenOnlyTopicLevelConnectionString_WhenReadingEffectiveConnectionString_ThenFallsBackToTopic()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "my-queue",
            ParentTopic = CreateTopic(serviceBusConnectionString: TopicConnectionString),
        };

        settings.EffectiveConnectionString.ShouldBe(TopicConnectionString);
    }

    [Fact]
    public void GivenOnlyTopicLevelNamespaceCredentials_WhenReadingEffectiveConnectionString_ThenBuildsItFromTopic()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "my-queue",
            ParentTopic = CreateTopicWithNamespaceCredentials(),
        };

        settings.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://topic-namespace.servicebus.windows.net/;SharedAccessKeyName=TopicKey;SharedAccessKey=topicabc123"
        );
    }

    [Fact]
    public void GivenOnlyTopicLevelConnectionString_WhenValidated_ThenNoException()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "my-queue",
            ParentTopic = CreateTopic(serviceBusConnectionString: TopicConnectionString),
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenOnlyTopicLevelNamespaceCredentials_WhenValidated_ThenNoException()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "my-queue",
            ParentTopic = CreateTopicWithNamespaceCredentials(),
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Theory]
    [InlineData(null, "TopicKey", "topicabc123")]
    [InlineData("topic-namespace", null, "topicabc123")]
    [InlineData("topic-namespace", "TopicKey", null)]
    public void GivenIncompleteTopicLevelNamespaceCredentials_WhenValidated_ThenThrowsAndHasNoEffectiveConnectionString(
        string? serviceBusNamespace,
        string? sharedAccessKeyName,
        string? sharedAccessKey
    )
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "my-queue",
            ParentTopic = CreateTopic(
                serviceBusNamespace: serviceBusNamespace,
                serviceBusSharedAccessKeyName: sharedAccessKeyName,
                serviceBusSharedAccessKey: sharedAccessKey
            ),
        };

        settings.EffectiveConnectionString.ShouldBeNull();
        Should.Throw<ArgumentException>(() => settings.Validate());
    }

    [Fact]
    public void GivenSubscriberAndTopicLevelConnectionStrings_WhenReadingEffectiveConnectionString_ThenSubscriberLevelWins()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
            ParentTopic = CreateTopic(serviceBusConnectionString: TopicConnectionString),
        };

        settings.EffectiveConnectionString.ShouldBe(SubscriberConnectionString);
    }

    [Fact]
    public void GivenSubscriberNamespaceCredentialsAndTopicLevelConnectionString_WhenReadingEffectiveConnectionString_ThenSubscriberLevelWins()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Namespace = "sub-namespace",
            SharedAccessKeyName = "SubKey",
            SharedAccessKey = "subabc123",
            Queue = "my-queue",
            ParentTopic = CreateTopic(serviceBusConnectionString: TopicConnectionString),
        };

        settings.EffectiveConnectionString.ShouldBe(
            "Endpoint=sb://sub-namespace.servicebus.windows.net/;SharedAccessKeyName=SubKey;SharedAccessKey=subabc123"
        );
    }

    [Fact]
    public void GivenTopicLevelConnectionStringAndNamespaceCredentials_WhenReadingEffectiveConnectionString_ThenTopicConnectionStringWins()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "my-queue",
            ParentTopic = CreateTopic(
                serviceBusConnectionString: TopicConnectionString,
                serviceBusNamespace: "other-namespace",
                serviceBusSharedAccessKeyName: "OtherKey",
                serviceBusSharedAccessKey: "otherabc123"
            ),
        };

        settings.EffectiveConnectionString.ShouldBe(TopicConnectionString);
    }

    [Fact]
    public void GivenOnlyEventHubCredentialsOnTopic_WhenValidated_ThenThrowsAndHasNoEffectiveConnectionString()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            Queue = "my-queue",
            ParentTopic = new TopicSettings
            {
                Name = "test-topic",
                Port = 60101,
                EventHubConnectionString = TopicConnectionString,
                EventHubNamespace = "topic-namespace",
                EventHubSharedAccessKeyName = "TopicKey",
                EventHubSharedAccessKey = "topicabc123",
            },
        };

        settings.EffectiveConnectionString.ShouldBeNull();
        Should.Throw<ArgumentException>(() => settings.Validate());
    }

    [Fact]
    public void GivenInvalidRetryPolicy_WhenValidated_ThenThrowsArgumentException()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "MaxDeliveryAttempts must be between 1 and 30. (Parameter 'MaxDeliveryAttempts')"
        );
    }

    // Validation order for Service Bus: name, authentication, topic/queue, properties, then
    // filter, retry policy and dead-letter. Each test below breaks two adjacent rules and
    // pins which message wins, word for word.

    [Fact]
    public void GivenBlankNameAndEverythingElseInvalid_WhenValidated_ThenNameErrorIsReported()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "   ",
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
    public void GivenConnectionStringAndNamespaceCredentialsAndBothTopicAndQueue_WhenValidated_ThenAuthenticationErrorIsReported()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Namespace = "my-namespace",
            SharedAccessKeyName = "RootManageSharedAccessKey",
            SharedAccessKey = "abc123",
            Queue = "my-queue",
            Topic = "my-topic",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Service Bus subscriber 'TestSubscriber' should specify either connectionString or namespace credentials, not both."
        );
    }

    [Fact]
    public void GivenConnectionStringAndOnlyPartOfNamespaceCredentials_WhenValidated_ThenNotBothErrorIsReported()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            SharedAccessKey = "abc123",
            Queue = "my-queue",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Service Bus subscriber 'TestSubscriber' should specify either connectionString or namespace credentials, not both."
        );
    }

    [Fact]
    public void GivenNoCredentialsAndNoDestination_WhenValidated_ThenAuthenticationErrorIsReported()
    {
        var settings = new ServiceBusSubscriberSettings { Name = "TestSubscriber" };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Service Bus subscriber 'TestSubscriber' must have either a connectionString or namespace + sharedAccessKeyName + sharedAccessKey, either at subscriber or topic level."
        );
    }

    [Fact]
    public void GivenNoDestinationAndInvalidProperty_WhenValidated_ThenDestinationErrorIsReported()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "", Value = "Subject" },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Service Bus subscriber 'TestSubscriber' must specify either a topic or queue."
        );
    }

    [Fact]
    public void GivenBothTopicAndQueueAndInvalidProperty_WhenValidated_ThenDestinationErrorIsReported()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
            Topic = "my-topic",
            Properties = new Dictionary<string, DeliveryPropertySettings>
            {
                ["Label"] = new() { Type = "", Value = "Subject" },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Service Bus subscriber 'TestSubscriber' must specify either a topic or queue, not both."
        );
    }

    [Fact]
    public void GivenInvalidPropertyAndInvalidFilter_WhenValidated_ThenPropertyErrorIsReported()
    {
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
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
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
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
        var settings = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = SubscriberConnectionString,
            Queue = "my-queue",
            DeadLetter = deadLetter,
        };

        settings.Validate();

        deadLetter.FolderPath.ShouldBe("./dead-letters");
    }
}
