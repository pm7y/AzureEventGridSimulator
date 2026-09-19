using System.Text;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Filtering;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Configuration;

/// <summary>
///     Loads settings through the real production path (<c>IConfiguration</c> +
///     <c>AddSimulatorSettings</c>) rather than <c>JsonSerializer</c>, because
///     <c>ConfigurationBinder</c> ignores the System.Text.Json attributes and
///     converters on the settings classes.
/// </summary>
[Trait("Category", "unit")]
public class AddSimulatorSettingsBindingTests
{
    private const string ServiceBusConnectionString =
        "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey";

    private static SimulatorSettings LoadSettings(IConfiguration configuration)
    {
        using var serviceProvider = new ServiceCollection()
            .AddSimulatorSettings(configuration)
            .BuildServiceProvider();

        return serviceProvider.GetRequiredService<SimulatorSettings>();
    }

    private static IConfigurationRoot BuildConfiguration(params string[] jsonDocuments)
    {
        var builder = new ConfigurationBuilder();
        var streams = jsonDocuments
            .Select(json => new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .ToList();

        foreach (var stream in streams)
        {
            builder.AddJsonStream(stream);
        }

        var configuration = builder.Build();

        foreach (var stream in streams)
        {
            stream.Dispose();
        }

        return configuration;
    }

    [Fact]
    public void AddSimulatorSettings_LegacyArrayFormat_ShouldBindHttpSubscribersWithAllProperties()
    {
        const string json = """
            {
                "topics": [{
                    "name": "LegacyTopic",
                    "port": 60101,
                    "key": "TheLocal+DevelopmentKey=",
                    "subscribers": [
                        {
                            "name": "FirstSubscriber",
                            "endpoint": "https://example.com/first",
                            "disableValidation": true,
                            "disabled": true,
                            "deliverySchema": "CloudEventV1_0",
                            "retryPolicy": {
                                "enabled": false,
                                "maxDeliveryAttempts": 5,
                                "eventTimeToLiveInMinutes": 60
                            },
                            "deadLetter": {
                                "enabled": false,
                                "folderPath": "./my-dead-letters"
                            },
                            "filter": {
                                "includedEventTypes": ["Some.Event"],
                                "subjectBeginsWith": "/orders",
                                "subjectEndsWith": ".json",
                                "isSubjectCaseSensitive": true,
                                "enableAdvancedFilteringOnArrays": true,
                                "advancedFilters": [{
                                    "operatorType": "StringIn",
                                    "key": "Data.colour",
                                    "values": ["red", "blue"]
                                }]
                            }
                        },
                        {
                            "name": "SecondSubscriber",
                            "endpoint": "https://example.com/second"
                        }
                    ]
                }]
            }
            """;

        var settings = LoadSettings(BuildConfiguration(json));

        var subscribers = settings.Topics.ShouldHaveSingleItem().Subscribers;
        subscribers.ServiceBusSubscribers.ShouldBeEmpty();
        subscribers.StorageQueueSubscribers.ShouldBeEmpty();
        subscribers.EventHubSubscribers.ShouldBeEmpty();

        var http = subscribers.HttpSubscribers.ToList();
        http.Select(s => s.Name).ShouldBe(["FirstSubscriber", "SecondSubscriber"]);
        http.Select(s => s.Endpoint)
            .ShouldBe(["https://example.com/first", "https://example.com/second"]);

        var first = http[0];
        first.DisableValidation.ShouldBeTrue();
        first.Disabled.ShouldBeTrue();
        first.DeliverySchema.ShouldBe(EventSchema.CloudEventV1_0);

        var retryPolicy = first.RetryPolicy.ShouldNotBeNullAnd();
        retryPolicy.Enabled.ShouldBeFalse();
        retryPolicy.MaxDeliveryAttempts.ShouldBe(5);
        retryPolicy.EventTimeToLiveInMinutes.ShouldBe(60);

        var deadLetter = first.DeadLetter.ShouldNotBeNullAnd();
        deadLetter.Enabled.ShouldBeFalse();
        deadLetter.FolderPath.ShouldBe("./my-dead-letters");

        var filter = first.Filter.ShouldNotBeNullAnd();
        filter.IncludedEventTypes.ShouldBe(["Some.Event"]);
        filter.SubjectBeginsWith.ShouldBe("/orders");
        filter.SubjectEndsWith.ShouldBe(".json");
        filter.IsSubjectCaseSensitive.ShouldBeTrue();
        filter.EnableAdvancedFilteringOnArrays.ShouldBeTrue();
        var advancedFilter = filter.AdvancedFilters.ShouldNotBeNullAnd().ShouldHaveSingleItem();
        advancedFilter.OperatorType.ShouldBe(
            AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn
        );
        advancedFilter.Key.ShouldBe("Data.colour");
        advancedFilter.Values.ShouldBe(["red", "blue"]);

        var second = http[1];
        second.DisableValidation.ShouldBeFalse();
        second.Disabled.ShouldBeFalse();
        second.DeliverySchema.ShouldBeNull();
        second.RetryPolicy.ShouldBeNull();
        second.DeadLetter.ShouldBeNull();
        second.Filter.ShouldBeNull();
    }

    [Fact]
    public void AddSimulatorSettings_ShippedAppSettingsJson_ShouldLoadOneSubscriberPerTopic()
    {
        // The simulator project's appsettings.json (legacy array format) is copied to the test output
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), false, false)
            .Build();

        var settings = LoadSettings(configuration);

        settings.Topics.Select(t => t.Name).ShouldBe(["EventGridTopic", "CloudEventsTopic"]);
        settings
            .Topics.Select(t => t.Subscribers.HttpSubscribers.ShouldHaveSingleItem().Name)
            .ShouldBe(["RequestCatcherSubscription", "CloudEventsRequestCatcherSubscription"]);
        settings
            .Topics.SelectMany(t => t.Subscribers.All)
            .ShouldAllBe(s => s.SubscriberType == "http");
    }

    [Fact]
    public void AddSimulatorSettings_ExampleAppSettingsJson_ShouldLoadLegacySubscribers()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "example.appsettings.json"),
                false,
                false
            )
            .Build();

        var settings = LoadSettings(configuration);

        settings
            .Topics.Select(t => $"{t.Name}={t.Subscribers.All.Count()}")
            .ShouldBe([
                "MyAwesomeTopic=1",
                "ATopicWithNoSubscribers=0",
                "ADisabledTopic=1",
                "RequestCatcherTopic=1",
            ]);
    }

    [Fact]
    public void AddSimulatorSettings_GroupedFormat_ShouldBindEverySubscriberType()
    {
        const string json = $$"""
            {
                "topics": [{
                    "name": "GroupedTopic",
                    "port": 60101,
                    "subscribers": {
                        "http": [{
                            "name": "HttpSubscriber",
                            "endpoint": "https://example.com/webhook",
                            "disableValidation": true
                        }],
                        "serviceBus": [{
                            "name": "ServiceBusSubscriber",
                            "connectionString": "{{ServiceBusConnectionString}}",
                            "queue": "orders",
                            "properties": {
                                "Source": { "type": "static", "value": "simulator" }
                            }
                        }],
                        "storageQueue": [{
                            "name": "StorageQueueSubscriber",
                            "connectionString": "UseDevelopmentStorage=true",
                            "queueName": "orders"
                        }],
                        "eventHub": [{
                            "name": "EventHubSubscriber",
                            "connectionString": "{{ServiceBusConnectionString}}",
                            "eventHubName": "orders-hub"
                        }]
                    }
                }]
            }
            """;

        var settings = LoadSettings(BuildConfiguration(json));

        var subscribers = settings.Topics.ShouldHaveSingleItem().Subscribers;
        subscribers
            .All.Select(s => $"{s.SubscriberType}:{s.Name}")
            .ShouldBe([
                "http:HttpSubscriber",
                "serviceBus:ServiceBusSubscriber",
                "storageQueue:StorageQueueSubscriber",
                "eventHub:EventHubSubscriber",
            ]);

        subscribers.HttpSubscribers.Single().Endpoint.ShouldBe("https://example.com/webhook");
        var serviceBus = subscribers.ServiceBusSubscribers.Single();
        serviceBus.Queue.ShouldBe("orders");
        serviceBus.Properties.ShouldNotBeNullAnd()["Source"].Value.ShouldBe("simulator");
        subscribers.StorageQueueSubscribers.Single().QueueName.ShouldBe("orders");
        subscribers.EventHubSubscribers.Single().EventHubName.ShouldBe("orders-hub");
    }

    [Fact]
    public void AddSimulatorSettings_LegacyAndGroupedKeysMergedFromTwoSources_ShouldUseGroupedFormat()
    {
        // e.g. the shipped legacy appsettings.json layered under a grouped appsettings.{env}.json:
        // the merged "subscribers" section then has both "0" and "http" children.
        const string legacyJson = """
            {
                "topics": [{
                    "name": "MergedTopic",
                    "port": 60101,
                    "subscribers": [{
                        "name": "LegacySubscriber",
                        "endpoint": "https://example.com/legacy"
                    }]
                }]
            }
            """;
        const string groupedJson = """
            {
                "topics": [{
                    "subscribers": {
                        "http": [{
                            "name": "GroupedSubscriber",
                            "endpoint": "https://example.com/grouped"
                        }]
                    }
                }]
            }
            """;

        var settings = LoadSettings(BuildConfiguration(legacyJson, groupedJson));

        settings
            .Topics.ShouldHaveSingleItem()
            .Subscribers.All.Select(s => s.Name)
            .ShouldBe(["GroupedSubscriber"]);
    }

    [Fact]
    public void AddSimulatorSettings_LegacySubscriberFromEnvironmentStyleKeys_ShouldBind()
    {
        // Mirrors AEGS_topics__0__subscribers__0__name=... after the environment variable provider
        // has stripped the prefix and translated "__" to ":"
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["topics:0:name"] = "EnvTopic",
                    ["topics:0:port"] = "60101",
                    ["topics:0:subscribers:0:name"] = "EnvSubscriber",
                    ["topics:0:subscribers:0:endpoint"] = "https://example.com/env",
                    ["topics:0:subscribers:0:disableValidation"] = "true",
                    ["topics:0:subscribers:1:name"] = "SecondEnvSubscriber",
                    ["topics:0:subscribers:1:endpoint"] = "https://example.com/env2",
                }
            )
            .Build();

        var settings = LoadSettings(configuration);

        var http = settings.Topics.ShouldHaveSingleItem().Subscribers.HttpSubscribers.ToList();
        http.Select(s => s.Name).ShouldBe(["EnvSubscriber", "SecondEnvSubscriber"]);
        http.Select(s => s.Endpoint)
            .ShouldBe(["https://example.com/env", "https://example.com/env2"]);
        http.Select(s => s.DisableValidation).ShouldBe([true, false]);
    }

    [Fact]
    public void AddSimulatorSettings_LegacySubscriberOverriddenByEnvironmentStyleKeys_ShouldApplyOverride()
    {
        const string json = """
            {
                "topics": [{
                    "name": "LegacyTopic",
                    "port": 60101,
                    "subscribers": [{
                        "name": "FileSubscriber",
                        "endpoint": "https://example.com/from-file",
                        "disableValidation": true
                    }]
                }]
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["topics:0:subscribers:0:endpoint"] = "https://example.com/from-env",
                }
            )
            .Build();

        var settings = LoadSettings(configuration);

        var subscriber = settings
            .Topics.ShouldHaveSingleItem()
            .Subscribers.HttpSubscribers.ShouldHaveSingleItem();
        subscriber.Name.ShouldBe("FileSubscriber");
        subscriber.Endpoint.ShouldBe("https://example.com/from-env");
        subscriber.DisableValidation.ShouldBeTrue();
    }

    private static string RangeSubscriber(string name, string destination)
    {
        var operatorType = name.EndsWith("NotInRange", StringComparison.Ordinal)
            ? "NumberNotInRange"
            : "NumberInRange";

        return $$"""
            {
                "name": "{{name}}",
                {{destination}},
                "filter": {
                    "advancedFilters": [{
                        "operatorType": "{{operatorType}}",
                        "key": "Data.x",
                        "values": [[0, 10], [20, 30]]
                    }]
                }
            }
            """;
    }

    private static string RangeSubscriberPair(string namePrefix, string destination)
    {
        return RangeSubscriber($"{namePrefix}InRange", destination)
            + ","
            + RangeSubscriber($"{namePrefix}NotInRange", destination);
    }

    private static string RangeFilterJson()
    {
        const string http = "\"endpoint\": \"https://example.com/webhook\"";
        const string serviceBus =
            $"\"connectionString\": \"{ServiceBusConnectionString}\", \"queue\": \"orders\"";
        const string storageQueue =
            "\"connectionString\": \"UseDevelopmentStorage=true\", \"queueName\": \"orders\"";
        const string eventHub =
            $"\"connectionString\": \"{ServiceBusConnectionString}\", \"eventHubName\": \"hub\"";

        return $$"""
            {
                "topics": [
                    {
                        "name": "LegacyTopic",
                        "port": 60101,
                        "subscribers": [{{RangeSubscriberPair("Legacy", http)}}]
                    },
                    {
                        "name": "GroupedTopic",
                        "port": 60102,
                        "subscribers": {
                            "http": [{{RangeSubscriberPair("Http", http)}}],
                            "serviceBus": [{{RangeSubscriberPair("ServiceBus", serviceBus)}}],
                            "storageQueue": [{{RangeSubscriberPair("StorageQueue", storageQueue)}}],
                            "eventHub": [{{RangeSubscriberPair("EventHub", eventHub)}}]
                        }
                    }
                ]
            }
            """;
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(15, false)]
    [InlineData(20, true)]
    [InlineData(25, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void AddSimulatorSettings_RangeFilterValues_ShouldEvaluateAsMinMaxPairsForEverySubscriberType(
        int x,
        bool isInRange
    )
    {
        var settings = LoadSettings(BuildConfiguration(RangeFilterJson()));
        var simulatorEvent = TestHelpers.CreateSimulatorEventFromEventGrid(data: new { x });

        var subscribers = settings.Topics.SelectMany(t => t.Subscribers.All).ToList();
        var wronglyEvaluated = subscribers
            .Where(s =>
            {
                var expected = s.Name.EndsWith("NotInRange", StringComparison.Ordinal)
                    ? !isInRange
                    : isInRange;
                return s.Filter.ShouldNotBeNullAnd().AcceptsEvent(simulatorEvent) != expected;
            })
            .Select(s => s.Name)
            .ToList();

        wronglyEvaluated.ShouldBeEmpty($"Data.x = {x}");
        subscribers
            .Select(s => s.Name)
            .ShouldBe(
                [
                    "LegacyInRange",
                    "LegacyNotInRange",
                    "HttpInRange",
                    "HttpNotInRange",
                    "ServiceBusInRange",
                    "ServiceBusNotInRange",
                    "StorageQueueInRange",
                    "StorageQueueNotInRange",
                    "EventHubInRange",
                    "EventHubNotInRange",
                ],
                ignoreOrder: true
            );
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(15, false)]
    [InlineData(25, true)]
    public void AddSimulatorSettings_RangeFilterFromEnvironmentStyleKeys_ShouldEvaluateAsMinMaxPairs(
        int x,
        bool isInRange
    )
    {
        const string filter = "topics:0:subscribers:0:filter:advancedFilters:0";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["topics:0:name"] = "EnvTopic",
                    ["topics:0:port"] = "60101",
                    ["topics:0:subscribers:0:name"] = "EnvSubscriber",
                    ["topics:0:subscribers:0:endpoint"] = "https://example.com/env",
                    [$"{filter}:operatorType"] = "NumberInRange",
                    [$"{filter}:key"] = "Data.x",
                    [$"{filter}:values:0:0"] = "0",
                    [$"{filter}:values:0:1"] = "10",
                    [$"{filter}:values:1:0"] = "20",
                    [$"{filter}:values:1:1"] = "30",
                }
            )
            .Build();

        var settings = LoadSettings(configuration);

        var subscriber = settings
            .Topics.ShouldHaveSingleItem()
            .Subscribers.HttpSubscribers.ShouldHaveSingleItem();
        subscriber
            .Filter.ShouldNotBeNullAnd()
            .AcceptsEvent(TestHelpers.CreateSimulatorEventFromEventGrid(data: new { x }))
            .ShouldBe(isInRange);
    }

    [Fact]
    public void AddSimulatorSettings_ScalarFilterValues_ShouldStillBindAsStrings()
    {
        const string json = """
            {
                "topics": [{
                    "name": "ScalarTopic",
                    "port": 60101,
                    "subscribers": {
                        "http": [{
                            "name": "ScalarSubscriber",
                            "endpoint": "https://example.com/webhook",
                            "filter": {
                                "advancedFilters": [
                                    {
                                        "operatorType": "NumberIn",
                                        "key": "Data.x",
                                        "values": [5, 7]
                                    },
                                    {
                                        "operatorType": "StringIn",
                                        "key": "Data.colour",
                                        "values": ["red", "blue"]
                                    }
                                ]
                            }
                        }]
                    }
                }]
            }
            """;

        var settings = LoadSettings(BuildConfiguration(json));

        var filter = settings
            .Topics.ShouldHaveSingleItem()
            .Subscribers.HttpSubscribers.ShouldHaveSingleItem()
            .Filter.ShouldNotBeNullAnd();
        var advancedFilters = filter.AdvancedFilters.ShouldNotBeNullAnd().ToList();
        advancedFilters[0].Values.ShouldBe(["5", "7"]);
        advancedFilters[1].Values.ShouldBe(["red", "blue"]);

        filter
            .AcceptsEvent(
                TestHelpers.CreateSimulatorEventFromEventGrid(data: new { x = 7, colour = "red" })
            )
            .ShouldBeTrue();
        filter
            .AcceptsEvent(
                TestHelpers.CreateSimulatorEventFromEventGrid(data: new { x = 6, colour = "red" })
            )
            .ShouldBeFalse();
        filter
            .AcceptsEvent(
                TestHelpers.CreateSimulatorEventFromEventGrid(data: new { x = 5, colour = "green" })
            )
            .ShouldBeFalse();
    }

    // ConfigurationBinder silently skips a collection element it cannot bind (e.g. an unknown
    // enum name), so the bound collections can be shorter than the configuration sections.
    // The elements after a skipped one must still keep their own settings.

    [Fact]
    public void AddSimulatorSettings_LegacyTopicDroppedByBinder_ShouldNotGiveItsSubscribersToTheNextTopic()
    {
        // "CloudEventSchemaV1_0" is Azure's name for the schema, not a simulator EventSchema
        const string json = """
            {
                "topics": [
                    {
                        "name": "OrdersTopic",
                        "port": 60101,
                        "inputSchema": "CloudEventSchemaV1_0",
                        "subscribers": [{
                            "name": "OrdersHook",
                            "endpoint": "https://example.com/orders"
                        }]
                    },
                    {
                        "name": "PaymentsTopic",
                        "port": 60102,
                        "subscribers": [{
                            "name": "PaymentsHook",
                            "endpoint": "https://example.com/payments"
                        }]
                    }
                ]
            }
            """;

        var settings = LoadSettings(BuildConfiguration(json));

        var topic = settings.Topics.ShouldHaveSingleItem();
        topic.Name.ShouldBe("PaymentsTopic");
        var subscriber = topic.Subscribers.HttpSubscribers.ShouldHaveSingleItem();
        subscriber.Name.ShouldBe("PaymentsHook");
        subscriber.Endpoint.ShouldBe("https://example.com/payments");
    }

    [Fact]
    public void AddSimulatorSettings_LegacyTopicDroppedByBinderBeforeGroupedTopic_ShouldKeepGroupedSubscribers()
    {
        const string json = """
            {
                "topics": [
                    {
                        "name": "LegacyTopic",
                        "port": "not-a-port",
                        "subscribers": [{
                            "name": "LegacyHook",
                            "endpoint": "https://example.com/legacy"
                        }]
                    },
                    {
                        "name": "GroupedTopic",
                        "port": 60102,
                        "subscribers": {
                            "http": [{
                                "name": "GroupedHook",
                                "endpoint": "https://example.com/grouped"
                            }]
                        }
                    }
                ]
            }
            """;

        var settings = LoadSettings(BuildConfiguration(json));

        var topic = settings.Topics.ShouldHaveSingleItem();
        topic.Name.ShouldBe("GroupedTopic");
        var subscriber = topic.Subscribers.All.ShouldHaveSingleItem();
        subscriber.Name.ShouldBe("GroupedHook");
        subscriber
            .ShouldBeOfType<HttpSubscriberSettings>()
            .Endpoint.ShouldBe("https://example.com/grouped");
    }

    private static string TopicWithTwoSubscribers(
        bool groupedFormat,
        string droppedSubscriber,
        string keptSubscriber
    )
    {
        var subscribers = groupedFormat
            ? $$"""{ "http": [{{droppedSubscriber}}, {{keptSubscriber}}] }"""
            : $"[{droppedSubscriber}, {keptSubscriber}]";

        return $$"""
            {
                "topics": [{
                    "name": "RangeTopic",
                    "port": 60101,
                    "subscribers": {{subscribers}}
                }]
            }
            """;
    }

    private static void ShouldOnlyAcceptZeroToTenAndTwentyToThirty(ISubscriberSettings subscriber)
    {
        var filter = subscriber.Filter.ShouldNotBeNullAnd();

        new[] { 5, 15, 25, 150 }
            .Select(x =>
                filter.AcceptsEvent(TestHelpers.CreateSimulatorEventFromEventGrid(data: new { x }))
            )
            .ShouldBe([true, false, true, false], "Data.x = 5, 15, 25, 150");
    }

    [Theory]
    [InlineData(false, "\"deliverySchema\": \"CloudEventSchemaV1_0\"")]
    [InlineData(true, "\"disabled\": \"nope\"")]
    public void AddSimulatorSettings_SubscriberDroppedByBinder_ShouldNotShiftNextSubscribersRangeFilter(
        bool groupedFormat,
        string unbindableProperty
    )
    {
        const string http = "\"endpoint\": \"https://example.com/webhook\"";
        var droppedSubscriber = $$"""
            {
                "name": "DroppedSubscriber",
                {{http}},
                {{unbindableProperty}},
                "filter": {
                    "advancedFilters": [{
                        "operatorType": "NumberInRange",
                        "key": "Data.x",
                        "values": [[100, 200]]
                    }]
                }
            }
            """;
        var json = TopicWithTwoSubscribers(
            groupedFormat,
            droppedSubscriber,
            RangeSubscriber("KeptSubscriber", http)
        );

        var settings = LoadSettings(BuildConfiguration(json));

        var subscriber = settings
            .Topics.ShouldHaveSingleItem()
            .Subscribers.All.ShouldHaveSingleItem();
        subscriber.Name.ShouldBe("KeptSubscriber");
        ShouldOnlyAcceptZeroToTenAndTwentyToThirty(subscriber);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddSimulatorSettings_ScalarSubscriberElementDroppedByBinder_ShouldNotShiftNextSubscribersRangeFilter(
        bool groupedFormat
    )
    {
        // The binder skips a scalar where an object is expected without throwing
        const string http = "\"endpoint\": \"https://example.com/webhook\"";
        var json = TopicWithTwoSubscribers(
            groupedFormat,
            "\"not-a-subscriber\"",
            RangeSubscriber("KeptSubscriber", http)
        );

        var settings = LoadSettings(BuildConfiguration(json));

        var subscriber = settings
            .Topics.ShouldHaveSingleItem()
            .Subscribers.All.ShouldHaveSingleItem();
        subscriber.Name.ShouldBe("KeptSubscriber");
        ShouldOnlyAcceptZeroToTenAndTwentyToThirty(subscriber);
    }

    [Fact]
    public void AddSimulatorSettings_AdvancedFilterDroppedByBinder_ShouldNotOverwriteNextFiltersValues()
    {
        const string json = """
            {
                "topics": [{
                    "name": "FilterTopic",
                    "port": 60101,
                    "subscribers": {
                        "http": [{
                            "name": "ColourSubscriber",
                            "endpoint": "https://example.com/webhook",
                            "filter": {
                                "advancedFilters": [
                                    {
                                        "operatorType": "NumberInRnage",
                                        "key": "Data.x",
                                        "values": [[0, 10]]
                                    },
                                    {
                                        "operatorType": "StringIn",
                                        "key": "Data.colour",
                                        "values": ["red"]
                                    }
                                ]
                            }
                        }]
                    }
                }]
            }
            """;

        var settings = LoadSettings(BuildConfiguration(json));

        var filter = settings
            .Topics.ShouldHaveSingleItem()
            .Subscribers.HttpSubscribers.ShouldHaveSingleItem()
            .Filter.ShouldNotBeNullAnd();
        var advancedFilter = filter.AdvancedFilters.ShouldNotBeNullAnd().ShouldHaveSingleItem();
        advancedFilter.OperatorType.ShouldBe(
            AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn
        );
        advancedFilter.Values.ShouldBe(["red"]);
        filter
            .AcceptsEvent(
                TestHelpers.CreateSimulatorEventFromEventGrid(data: new { colour = "red" })
            )
            .ShouldBeTrue();
        filter
            .AcceptsEvent(
                TestHelpers.CreateSimulatorEventFromEventGrid(data: new { colour = "blue" })
            )
            .ShouldBeFalse();
    }
}
