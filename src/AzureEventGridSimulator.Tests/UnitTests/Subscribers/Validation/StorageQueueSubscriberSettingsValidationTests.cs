using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class StorageQueueSubscriberSettingsValidationTests
{
    private static StorageQueueSubscriberSettings CreateValidSettings()
    {
        return new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
        };
    }

    private static TopicSettings CreateTopicWithConnectionString()
    {
        return new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TheLocal+DevelopmentKey=",
            StorageQueueConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=topicstorage;AccountKey=xyz789;EndpointSuffix=core.windows.net",
        };
    }

    [Fact]
    public void Validate_WithConnectionStringAndQueue_ShouldPass()
    {
        var settings = CreateValidSettings();

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithTopicLevelConnectionString_ShouldPass()
    {
        var topic = CreateTopicWithConnectionString();
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            QueueName = "my-queue",
            ParentTopic = topic,
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithoutName_ShouldThrow()
    {
        var json = """
            {
                "connectionString": "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
                "queueName": "my-queue"
            }
            """;

        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<StorageQueueSubscriberSettings>(json)
        );
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldThrow()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "   ",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("name");
    }

    [Fact]
    public void Validate_WithoutConnectionString_ShouldThrow()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            QueueName = "my-queue",
            // No ConnectionString and no ParentTopic with connection string
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("connectionString");
    }

    [Fact]
    public void Validate_WithoutQueueName_ShouldThrow()
    {
        var json = """
            {
                "name": "TestSubscriber",
                "connectionString": "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net"
            }
            """;

        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<StorageQueueSubscriberSettings>(json)
        );
    }

    [Fact]
    public void Validate_WithEmptyQueueName_ShouldThrow()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "   ",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("queueName");
    }

    [Fact]
    public void Validate_WithValidFilter_ShouldPass()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
            Filter = new FilterSetting
            {
                IncludedEventTypes = ["MyEvent"],
                SubjectBeginsWith = "test/",
            },
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void EffectiveConnectionString_WithSubscriberConnectionString_ShouldReturnIt()
    {
        var settings = CreateValidSettings();

        settings.EffectiveConnectionString.ShouldBe(settings.ConnectionString);
    }

    [Fact]
    public void EffectiveConnectionString_WithTopicConnectionString_ShouldReturnIt()
    {
        var topic = CreateTopicWithConnectionString();
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            QueueName = "my-queue",
            ParentTopic = topic,
        };

        settings.EffectiveConnectionString.ShouldBe(topic.StorageQueueConnectionString);
    }

    [Fact]
    public void EffectiveConnectionString_WithBothConnectionStrings_ShouldPreferSubscriberLevel()
    {
        var topic = CreateTopicWithConnectionString();
        var settings = CreateValidSettings();
        settings.ParentTopic = topic;

        // Subscriber-level connection string should take precedence
        settings.EffectiveConnectionString.ShouldBe(settings.ConnectionString);
    }

    [Fact]
    public void SubscriberType_ShouldReturnStorageQueue()
    {
        var settings = CreateValidSettings();

        settings.SubscriberType.ShouldBe("storageQueue");
    }

    [Fact]
    public void Disabled_DefaultValue_ShouldBeFalse()
    {
        var settings = CreateValidSettings();

        settings.Disabled.ShouldBeFalse();
    }

    [Fact]
    public void Disabled_WhenSetToTrue_ShouldBeTrue()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
            Disabled = true,
        };

        settings.Disabled.ShouldBeTrue();
    }

    [Fact]
    public void DeliverySchema_DefaultValue_ShouldBeNull()
    {
        var settings = CreateValidSettings();

        settings.DeliverySchema.ShouldBeNull();
    }

    [Fact]
    public void Filter_DefaultValue_ShouldBeNull()
    {
        var settings = CreateValidSettings();

        settings.Filter.ShouldBeNull();
    }

    [Fact]
    public void GivenInvalidRetryPolicy_WhenValidated_ThenThrowsArgumentException()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "MaxDeliveryAttempts must be between 1 and 30. (Parameter 'MaxDeliveryAttempts')"
        );
    }

    // Validation order for Storage Queue: name, connectionString, queueName, then filter,
    // retry policy and dead-letter. Each test below breaks two adjacent rules and pins which
    // message wins, word for word.

    [Fact]
    public void GivenBlankNameAndEverythingElseInvalid_WhenValidated_ThenNameErrorIsReported()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "   ",
            QueueName = "   ",
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe("Subscriber name is required. (Parameter 'Name')");
    }

    [Fact]
    public void GivenNoConnectionStringAndBlankQueueName_WhenValidated_ThenConnectionStringErrorIsReported()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            QueueName = "   ",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Storage Queue subscriber 'TestSubscriber' must have a connectionString, either at subscriber or topic level."
        );
    }

    [Fact]
    public void GivenBlankQueueNameAndInvalidFilter_WhenValidated_ThenQueueNameErrorIsReported()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "   ",
            Filter = new FilterSetting { AdvancedFilters = [new AdvancedFilterSetting()] },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Storage Queue subscriber 'TestSubscriber' must have a queueName."
        );
    }

    [Fact]
    public void GivenInvalidFilterAndInvalidRetryPolicy_WhenValidated_ThenFilterErrorIsReported()
    {
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
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
        var settings = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
            DeadLetter = deadLetter,
        };

        settings.Validate();

        deadLetter.FolderPath.ShouldBe("./dead-letters");
    }
}
