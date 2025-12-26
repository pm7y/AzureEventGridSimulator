using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class ServiceBusSubscriberSettingsValidationTests
{
    private static ServiceBusSubscriberSettings CreateValidConnectionStringSettings(
        string? queue = "my-queue",
        string? topic = null
    )
    {
        return new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Queue = "my-queue",
            Filter = new FilterSetting
            {
                IncludedEventTypes = new List<string> { "MyEvent" },
                SubjectBeginsWith = "test/",
            },
        };

        Should.NotThrow(() => settings.Validate());
    }
}
