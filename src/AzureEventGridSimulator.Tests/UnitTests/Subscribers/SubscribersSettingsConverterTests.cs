using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

[Trait("Category", "unit")]
public class SubscribersSettingsConverterTests
{
    [Fact]
    public void LegacyArrayFormat_ShouldDeserializeAsHttpSubscribers()
    {
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": [
            {
                ""name"": ""LegacySubscriber"",
                ""endpoint"": ""https://example.com/webhook""
            }
        ]
    }]
}";

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
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": [
            {
                ""name"": ""Subscriber1"",
                ""endpoint"": ""https://example.com/webhook1""
            },
            {
                ""name"": ""Subscriber2"",
                ""endpoint"": ""https://example.com/webhook2""
            }
        ]
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.Topics.First().Subscribers.HttpSubscribers.Count().ShouldBe(2);
    }

    [Fact]
    public void NewGroupedFormat_WithOnlyHttpSubscribers_ShouldDeserialize()
    {
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": {
            ""http"": [
                {
                    ""name"": ""HttpSubscriber"",
                    ""endpoint"": ""https://example.com/webhook""
                }
            ]
        }
    }]
}";

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
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": {
            ""serviceBus"": [
                {
                    ""name"": ""ServiceBusSubscriber"",
                    ""connectionString"": ""Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123"",
                    ""queue"": ""my-queue""
                }
            ]
        }
    }]
}";

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
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": {
            ""http"": [
                {
                    ""name"": ""HttpSubscriber"",
                    ""endpoint"": ""https://example.com/webhook""
                }
            ],
            ""serviceBus"": [
                {
                    ""name"": ""ServiceBusSubscriber"",
                    ""connectionString"": ""Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123"",
                    ""topic"": ""my-topic""
                }
            ]
        }
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.ShouldNotBeNull();
        var topic = settings.Topics.First();
        topic.Subscribers.HttpSubscribers.ShouldHaveSingleItem();
        topic.Subscribers.ServiceBusSubscribers.ShouldHaveSingleItem();
    }

    [Fact]
    public void ServiceBusSubscriber_WithNamespaceCredentials_ShouldDeserialize()
    {
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": {
            ""serviceBus"": [
                {
                    ""name"": ""ServiceBusSubscriber"",
                    ""namespace"": ""my-namespace"",
                    ""sharedAccessKeyName"": ""RootManageSharedAccessKey"",
                    ""sharedAccessKey"": ""abc123"",
                    ""queue"": ""my-queue""
                }
            ]
        }
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var sbSub = settings.Topics.First().Subscribers.ServiceBusSubscribers.First();
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
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": {
            ""serviceBus"": [
                {
                    ""name"": ""ServiceBusSubscriber"",
                    ""connectionString"": ""Endpoint=sb://ns.servicebus.windows.net/"",
                    ""queue"": ""my-queue"",
                    ""properties"": {
                        ""Label"": { ""type"": ""dynamic"", ""value"": ""Subject"" },
                        ""Region"": { ""type"": ""static"", ""value"": ""west-us"" }
                    }
                }
            ]
        }
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var sbSub = settings.Topics.First().Subscribers.ServiceBusSubscribers.First();
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
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": []
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.Topics.First().Subscribers.HttpSubscribers.ShouldBeEmpty();
        settings.Topics.First().Subscribers.ServiceBusSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void EmptySubscribersObject_ShouldDeserializeToEmptyLists()
    {
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": {}
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.Topics.First().Subscribers.HttpSubscribers.ShouldBeEmpty();
        settings.Topics.First().Subscribers.ServiceBusSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void AllSubscribers_ShouldReturnCombinedList()
    {
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": {
            ""http"": [
                { ""name"": ""Http1"", ""endpoint"": ""https://a.com"" },
                { ""name"": ""Http2"", ""endpoint"": ""https://b.com"" }
            ],
            ""serviceBus"": [
                { ""name"": ""SB1"", ""connectionString"": ""Endpoint=sb://ns.servicebus.windows.net/"", ""queue"": ""q1"" }
            ]
        }
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var allSubscribers = settings.Topics.First().Subscribers.All.ToList();
        allSubscribers.Count.ShouldBe(3);
        allSubscribers.Select(s => s.Name).ShouldBe(new[] { "Http1", "Http2", "SB1" });
    }

    [Fact]
    public void Serialization_ShouldWriteInNewFormat()
    {
        var settings = new SubscribersSettings
        {
            Http = new[]
            {
                new HttpSubscriberSettings { Name = "HttpSub", Endpoint = "https://example.com" },
            },
            ServiceBus = new[]
            {
                new ServiceBusSubscriberSettings
                {
                    Name = "SBSub",
                    ConnectionString = "Endpoint=sb://ns.servicebus.windows.net/",
                    Queue = "my-queue",
                },
            },
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
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": [
            {
                ""name"": ""FilteredSubscriber"",
                ""endpoint"": ""https://example.com/webhook"",
                ""filter"": {
                    ""includedEventTypes"": [""MyEvent""],
                    ""subjectBeginsWith"": ""test/"",
                    ""subjectEndsWith"": null,
                    ""isSubjectCaseSensitive"": false,
                    ""advancedFilters"": null
                }
            }
        ]
    }]
}";

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        var httpSub = settings.Topics.First().Subscribers.HttpSubscribers.First();
        httpSub.Filter.ShouldNotBeNull();
        httpSub.Filter.IncludedEventTypes.ShouldContain("MyEvent");
        httpSub.Filter.SubjectBeginsWith.ShouldBe("test/");
    }

    [Fact]
    public void InvalidTokenType_ShouldThrowJsonException()
    {
        const string json =
            @"
{
    ""topics"": [{
        ""name"": ""MyTopic"",
        ""port"": 60101,
        ""key"": ""TheKey="",
        ""subscribers"": ""invalid_string""
    }]
}";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SimulatorSettings>(json));
    }
}
