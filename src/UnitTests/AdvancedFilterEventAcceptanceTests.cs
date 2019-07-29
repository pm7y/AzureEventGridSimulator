using System;
using AzureEventGridSimulator;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Xunit;

namespace Tests
{
    public class AdvancedFilterEventAcceptanceTests
    {
        private static readonly EventGridEvent _gridEvent = new EventGridEvent
        {
            Id = "EventId",
            Data = new { NumberValue = 1, IsTrue = true, Name = "StringValue", DoubleValue = 0.12345d, NumberMaxValue = ulong.MaxValue },
            DataVersion = "5.0",
            EventTime = DateTime.UtcNow.ToString(),
            EventType = "this.is.a.test.event.type",
            MetadataVersion = "2.3.4",
            Subject = "TheEventSubject",
            Topic = "THE_EVENT_TOPIC"
        };

        [Theory]
        [ClassData(typeof(PositiveFilterTestCaseContainer))]
        public void TestAdvancedFilteringSuccess(AdvancedFilterSetting filter)
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new AdvancedFilterSetting[] { filter } };
            Assert.True(filterConfig.AcceptsEvent(_gridEvent), $"{filter.Key} - {filter.OperatorType} - {filter.Value} - {filter.Values.Separate() }");
        }

        [Theory]
        [ClassData(typeof(NegativeFilterTestCaseContainer))]
        public void TestAdvancedFilteringFailure(AdvancedFilterSetting filter)
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new AdvancedFilterSetting[] { filter } };
            Assert.False(filterConfig.AcceptsEvent(_gridEvent), $"{filter.Key} - {filter.OperatorType} - {filter.Value} - {filter.Values.Separate() }");
        }
    }
}
