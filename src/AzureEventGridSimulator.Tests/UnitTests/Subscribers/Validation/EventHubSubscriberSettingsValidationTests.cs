using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class EventHubSubscriberSettingsValidationTests
{
    private static EventHubSubscriberSettings CreateValidConnectionStringSettings()
    {
        return new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
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
        var settings = CreateValidConnectionStringSettings();
        settings.Name = null;

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.ParamName.ShouldBe("Name");
        exception.Message.ShouldContain("name is required");
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldThrow()
    {
        var settings = CreateValidConnectionStringSettings();
        settings.Name = "   ";

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.ParamName.ShouldBe("Name");
    }

    [Fact]
    public void Validate_WithoutEventHubName_ShouldThrow()
    {
        var settings = CreateValidConnectionStringSettings();
        settings.EventHubName = null;

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("eventHubName");
    }

    [Fact]
    public void Validate_WithEmptyEventHubName_ShouldThrow()
    {
        var settings = CreateValidConnectionStringSettings();
        settings.EventHubName = "   ";

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
        var settings = CreateValidConnectionStringSettings();
        settings.Namespace = "my-namespace";
        settings.SharedAccessKeyName = "RootManageSharedAccessKey";
        settings.SharedAccessKey = "abc123";

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
        var settings = CreateValidConnectionStringSettings();
        settings.Properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Label"] = new() { Type = "dynamic", Value = "Subject" },
            ["Region"] = new() { Type = "static", Value = "west-us" },
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithInvalidPropertyType_ShouldThrow()
    {
        var settings = CreateValidConnectionStringSettings();
        settings.Properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Label"] = new() { Type = "invalid", Value = "Subject" },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Label");
        exception.Message.ShouldContain("static");
        exception.Message.ShouldContain("dynamic");
    }

    [Fact]
    public void Validate_WithEmptyPropertyType_ShouldThrow()
    {
        var settings = CreateValidConnectionStringSettings();
        settings.Properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Label"] = new() { Type = "", Value = "Subject" },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Label");
        exception.Message.ShouldContain("type");
    }

    [Fact]
    public void Validate_WithEmptyPropertyValue_ShouldThrow()
    {
        var settings = CreateValidConnectionStringSettings();
        settings.Properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Label"] = new() { Type = "static", Value = "" },
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
        var settings = CreateValidConnectionStringSettings();
        settings.Filter = new FilterSetting
        {
            IncludedEventTypes = new List<string> { "MyEvent" },
            SubjectBeginsWith = "test/",
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void Validate_WithTopicLevelConnectionString_ShouldPass()
    {
        var parentTopic = new TopicSettings
        {
            Name = "test-topic",
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
}
