using System.Linq;
using AzureEventGridSimulator.Settings;
using Newtonsoft.Json;
using Xunit;

namespace UnitTests
{
    public class ConfigurationLoadingTests
    {
        [Fact]
        public void TestConfigurationLoad()
        {
            var json = "{\"topics\":[{\"name\":\"MyAwesomeTopic\",\"port\":60101,\"key\":\"TheLocal+DevelopmentKey=\",\"subscribers\":[{\"name\":\"LocalAzureFunctionSubscription\",\"endpoint\":\"http://localhost:7071/runtime/webhooks/EventGrid?functionName=PersistEventToDb\",\"filter\":{\"includedEventTypes\":[\"some.special.event.type\"],\"subjectBeginsWith\":\"MySubject\",\"subjectEndsWith\":\"_success\",\"isSubjectCaseSensitive\":true,\"advancedFilters\":[{\"operatorType\":\"NumberGreaterThanOrEquals\",\"key\":\"Data.Key1\",\"value\":5},{\"operatorType\":\"StringContains\",\"key\":\"Subject\",\"values\":[\"container1\",\"container2\"]}]}}]},{\"name\":\"ATopicWithNoSubscribers\",\"port\":60102,\"key\":\"TheLocal+DevelopmentKey=\",\"subscribers\":[]}]}";

            var settings = JsonConvert.DeserializeObject<SimulatorSettings>(json);
            Assert.NotNull(settings);
            Assert.NotNull(settings.Topics);
            Assert.True(settings.Topics.All(t => t.Subscribers.All(s => s.Filter?.AdvancedFilters != null)));
            settings.Validate();
        }
    }
}
