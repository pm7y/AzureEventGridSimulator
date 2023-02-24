using System.Linq;
using AzureEventGridSimulator.Infrastructure.Settings;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests;

[Trait("Category", "unit")]
public class ConfigurationLoadingTests
{
    [Fact]
    public void TestConfigurationLoad()
    {
        const string json = @"
        {
            ""topics"": [{
                ""name"": ""MyAwesomeTopic"",
                ""port"": 60101,
                ""key"": ""TheLocal+DevelopmentKey="",
                ""type"": ""EventGridEvent"",
                ""subscribers"": {
                    ""http"": [{
                        ""name"": ""LocalAzureFunctionSubscription"",
                        ""endpoint"":""http://localhost:7071/runtime/webhooks/EventGrid?functionName=PersistEventToDb"",
                        ""filter"": {
                            ""includedEventTypes"":[""some.special.event.type""],
                            ""subjectBeginsWith"":""MySubject"",
                            ""subjectEndsWith"":""_success"",
                            ""isSubjectCaseSensitive"":true,
                            ""advancedFilters"": [{
                                    ""operatorType"":""NumberGreaterThanOrEquals"",
                                    ""key"":""Data.Key1"",
                                    ""value"":5
                                },
                                {
                                    ""operatorType"":""StringContains"",
                                    ""key"":""Subject"",
                                    ""values"":[""container1"",""container2""
                                ]}
                            ]}
                    }],
                    ""serviceBus"": [
                      {
                        ""name"": ""AzureServiceBus"",
                        ""namespace"": ""azure-servicebus-namespace"",
                        ""sharedAccessKeyName"": ""RootManageSharedAccessKey"",
                        ""sharedAccessKey"": ""VGhlcmUgcmVhbGx5IGlzIG5vdGhpbmcgdG8gc2VlIGhlcmU="",
                        ""topic"": ""azure-servicebus-topic"",
                        ""properties"": {
                          ""MessgeId"": {
                            ""type"": ""dynamic"",
                            ""value"": ""Id""
                          },
                          ""Label"": {
                            ""type"": ""dynamic"",
                            ""value"": ""Subject""
                          }
                        }
                      }
                    ]
                }   
            },
            {
                ""name"":""ATopicWithNoSubscribers"",
                ""port"":60102,
                ""key"":""TheLocal+DevelopmentKey="",
                ""type"": ""CloudEvent"",
                ""subscribers"": {}
            }]
        }";

        var settings = JsonConvert.DeserializeObject<SimulatorSettings>(json);

        settings.ShouldNotBeNull();
        settings.Topics.ShouldNotBeNull();
        settings.Topics.ShouldAllBe(t =>
                                        t.Subscribers.Http.All(s => s.Filter != null) &&
                                        t.Subscribers.Http.All(s => s.Filter.AdvancedFilters != null)
                                   );

        Should.NotThrow(() => { settings.Validate(); });
    }
}
