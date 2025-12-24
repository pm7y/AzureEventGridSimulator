using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Configuration;

[Trait("Category", "unit")]
public class ConfigurationLoadingTests
{
    [Fact]
    public void IConfigurationBind_ShouldLoadEventHubSubscribers()
    {
        const string json = """
            {
                "topics": [{
                    "name": "TestTopic",
                    "port": 60101,
                    "key": "TheLocal+DevelopmentKey=",
                    "subscribers": {
                        "http": [{
                            "name": "HttpSubscriber",
                            "endpoint": "https://example.com/webhook",
                            "disableValidation": true
                        }],
                        "eventHub": [{
                            "name": "EventHubSubscriber",
                            "connectionString": "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                            "eventHubName": "test-hub"
                        }]
                    }
                }]
            }
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();

        var settings = new SimulatorSettings();
        configuration.Bind(settings);

        settings.ShouldNotBeNull();
        settings.Topics.ShouldNotBeNull();
        settings.Topics.Length.ShouldBe(1);

        var topic = settings.Topics.First();
        topic.Subscribers.HttpSubscribers.Count().ShouldBe(1);
        topic.Subscribers.HttpSubscribers.First().Name.ShouldBe("HttpSubscriber");

        topic.Subscribers.EventHubSubscribers.Count().ShouldBe(1);
        var eventHubSubscriber = topic.Subscribers.EventHubSubscribers.First();
        eventHubSubscriber.Name.ShouldBe("EventHubSubscriber");
        eventHubSubscriber.EventHubName.ShouldBe("test-hub");
        eventHubSubscriber
            .ConnectionString.ShouldNotBeNullAnd()
            .ShouldContain("sb://test.servicebus.windows.net");
    }

    [Fact]
    public void JsonDeserialize_LegacyFormat_ShouldLoadHttpSubscribers()
    {
        // This test uses the legacy format (array of subscribers) to verify backwards compatibility
        const string json = """

            {
                "topics": [{
                    "name": "MyAwesomeTopic",
                    "port": 60101,
                    "key": "TheLocal+DevelopmentKey=",
                    "subscribers": [{
                        "name": "LocalAzureFunctionSubscription",
                        "endpoint":"http://localhost:7071/runtime/webhooks/EventGrid?functionName=PersistEventToDb",
                        "filter": {
                            "includedEventTypes":["some.special.event.type"],
                            "subjectBeginsWith":"MySubject",
                            "subjectEndsWith":"_success",
                            "isSubjectCaseSensitive":true,
                            "advancedFilters": [{
                                "operatorType":"NumberGreaterThanOrEquals",
                                "key":"Data.Key1",
                                "value":5
                            },
                            {
                                "operatorType":"StringContains",
                                "key":"Subject",
                                "values":["container1","container2"
                            ]}
                        ]}
                    }]
                },
                {
                    "name":"ATopicWithNoSubscribers",
                    "port":60102,
                    "key":"TheLocal+DevelopmentKey=",
                    "subscribers":[]
                }]
            }
            """;

        var settings = JsonSerializer.Deserialize<SimulatorSettings>(json);

        settings.ShouldNotBeNull();
        settings.Topics.ShouldNotBeNull();

        // Verify the legacy format was correctly converted to HTTP subscribers
        var topicWithSubscribers = settings.Topics.First();
        topicWithSubscribers.Subscribers.HttpSubscribers.ShouldNotBeEmpty();
        topicWithSubscribers
            .Subscribers.HttpSubscribers.All(s => s.Filter is { AdvancedFilters: not null })
            .ShouldBeTrue();

        Should.NotThrow(() =>
        {
            settings.Validate();
        });
    }
}
