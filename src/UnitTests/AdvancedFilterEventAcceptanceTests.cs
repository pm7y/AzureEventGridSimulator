using System;
using AzureEventGridSimulator;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using NUnit.Framework;
using static AzureEventGridSimulator.Settings.AdvancedFilterSetting;

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

        #region Positive Configurations

        private static AdvancedFilterSetting[] GetPositiveIdFilterConfigurations()
        {
            return new AdvancedFilterSetting[]
            {
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = string.Empty },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "E" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "Event" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "Event" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "EVE" }, // according to the spec, string comparisons in advanced mode are always case insensitive
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "E" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "ent" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "ENT" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "d" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "EventId" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "EventId" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = string.Empty },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "Id" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "d" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "D" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"EventId" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"eventid" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"EVENTID" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"different", "EVENTID", "Another" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] {"different", "EVENTID", "Another" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringNotIn, Values = new string[] {"different", "notfound", "Another" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringNotIn, Values = null},
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringNotIn, Values = new string[0]}
            };
        }

        private static AdvancedFilterSetting[] GetPositiveTopicFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="THE" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="the_" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringContains, Value ="event" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="Ic" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] {"the_event_topic" } },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] {"not_the_right_one" } }
            };
        }

        private static AdvancedFilterSetting[] GetPositiveSubjectFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="THE" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="theE" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringContains, Value ="event" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="Subject" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] { "theeventsubject" } },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "NotTheEventSubject" } }
            };
        }

        private static AdvancedFilterSetting[] GetPositiveEventTypeFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="this" },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="ThIs" },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringContains, Value =".event." },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="EVENT.TYPE" },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] { "this.is.a.test.event.type" } },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "Not-the-right-type" } }
            };
        }

        private static AdvancedFilterSetting[] GetPositiveDataVersionFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="5" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringContains, Value ="." },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="0" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] { "5.0" } },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "5" } }
                          };
        }

        private static AdvancedFilterSetting[] GetPositiveEventDataFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 0 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 0.5 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 0.5 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 1 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object []{ 1.0, 2, 3.5, "stringValue", true } },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object []{ 0, 2, 3.5, "stringValue", true } },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 1.1 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 2 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 2 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 2 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 1 },
               new AdvancedFilterSetting { Key = "Data.IsTrue", OperatorType = OperatorTypeEnum.BoolEquals, Value = true },
               new AdvancedFilterSetting { Key = "Data.Name", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "StringValue"},
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 0.123449f},
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 0.12345f},
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object[] { 0.12345d } },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 0.123451d },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 0.12345d },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object[] { 0.123451d } },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = long.MaxValue },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = ulong.MaxValue },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object[] { ulong.MaxValue } },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = ulong.MaxValue },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object[] { ulong.MaxValue } },
                           };
        }

        private static AdvancedFilterSetting[] GetPositiveEventIdFilterConfigurations()
        {
            // no positive tests are available for the 'EventId' key yet since no support for the cloud event schema is available at the moment
            // special case for use with the cloud event schema (https://docs.microsoft.com/en-us/azure/event-grid/cloudevents-schema)
            Assert.Inconclusive("Requires support for the cloud event schema to be implemented (https://docs.microsoft.com/en-us/azure/event-grid/cloudevents-schema)");
            return new AdvancedFilterSetting[] { };
        }

        private static AdvancedFilterSetting[] GetPositiveSourceFilterConfigurations()
        {
            // special case for use with the cloud event schema (https://docs.microsoft.com/en-us/azure/event-grid/cloudevents-schema)
            // no positive tests are available for the 'Source' key yet since no support for the cloud event schema is available at the moment
            Assert.Inconclusive("Requires support for the cloud event schema to be implemented (https://docs.microsoft.com/en-us/azure/event-grid/cloudevents-schema)");
            return new AdvancedFilterSetting[] { };
        }

        private static AdvancedFilterSetting[] GetPositiveEventTypeVersionFilterConfigurations()
        {
            // special case for use with the cloud event schema (https://docs.microsoft.com/en-us/azure/event-grid/cloudevents-schema)
            // no positive tests are available for the 'EventTypeVersion' key yet since no support for the cloud event schema is available at the moment
            Assert.Inconclusive("Requires support for the cloud event schema to be implemented (https://docs.microsoft.com/en-us/azure/event-grid/cloudevents-schema)");
            return new AdvancedFilterSetting[] { };
        }

        #endregion

        private static AdvancedFilterSetting[] GetNegativeIdFilterConfigurations()
        {
            return new AdvancedFilterSetting[]
            {
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = string.Empty },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "A" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "a" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "a" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "TEN" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "b" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = string.Empty },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "B" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"notCorrect" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"different", "not_found", "Another" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringNotIn, Values = new string[] {"different", "EventID", "Another" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringNotIn, Values = new string[] {"different", "EventId", "Another" } }
            };
        }

        private static AdvancedFilterSetting[] GetNegativeTopicFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="HE" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="he_" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringContains, Value ="everest" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="123" },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] {"_event_" } },
               new AdvancedFilterSetting { Key = "Topic", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "THE_EVENT_TOPIC" } }
            };
        }

        private static AdvancedFilterSetting[] GetNegativeSubjectFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="E" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="able" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringContains, Value ="x" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="sub" },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] { "not_correct" } },
               new AdvancedFilterSetting { Key = "Subject", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "theeventsubject" } }
            };
        }

        private static AdvancedFilterSetting[] GetNegativeEventTypeFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="his" },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="hIs" },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringContains, Value =".." },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="EVENTTYPE" },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringIn, Values = new object[0] },
               new AdvancedFilterSetting { Key = "EventType", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "Not-the-right-type", "this.is.a.test.event.type" } }
            };
        }

        private static AdvancedFilterSetting[] GetNegativeDataVersionFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="5" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringContains, Value ="_" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="7" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] { "5.0.1" } },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "5.0" } }
                          };
        }

        private static AdvancedFilterSetting[] GetNegativeEventDataFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 0 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 1 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 1.01 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 5 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object []{ 1.1, 2, 3.5, "stringValue", true } },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object []{ 0, 1, 2, 3.5, "stringValue", true } },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 1 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 0.99999999 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 0.9 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = -1 },
               new AdvancedFilterSetting { Key = "Data.IsTrue", OperatorType = OperatorTypeEnum.BoolEquals, Value = false },
               new AdvancedFilterSetting { Key = "Data.Name", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "String_Value"},
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 0.12345},
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 0.123451},
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object[] { 0.123451 } },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 0.12345 },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 0.1234 },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object[] { 0.12345 } },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = ulong.MaxValue },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = long.MaxValue },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object[] { long.MaxValue } },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = long.MaxValue },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = ulong.MaxValue },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object[] { ulong.MaxValue } },
            };
        }

        private static AdvancedFilterSetting[] GetNegativeEventIdFilterConfigurations()
        {
            // everything with this key is considered negative at the moment given that the key will never be found on an event that doesn not conform to the cloud schema
            // special case for use with the cloud event schema (https://docs.microsoft.com/en-us/azure/event-grid/cloudevents-schema)
            return new AdvancedFilterSetting[] {
                new AdvancedFilterSetting { Key = "EventId" }
            };
        }

        private static AdvancedFilterSetting[] GetNegativeSourceFilterConfigurations()
        {
            // everything with this key is considered negative at the moment given that the key will never be found on an event that doesn not conform to the cloud schema
            // no positive tests are available for this key yet since no support for the cloud event schema is available at the moment
            return new AdvancedFilterSetting[] {
                new AdvancedFilterSetting { Key = "Source" }
            };
        }

        private static AdvancedFilterSetting[] GetNegativeEventTypeVersionFilterConfigurations()
        {
            // everything with this key is considered negative at the moment given that the key will never be found on an event that doesn not conform to the cloud schema
            // no positive tests are available for this key yet since no support for the cloud event schema is available at the moment
            return new AdvancedFilterSetting[] {
                new AdvancedFilterSetting { Key = "EventTypeVersion" }
            };
        }

        [TestCaseSource(nameof(GetPositiveIdFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveDataVersionFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveEventDataFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveEventIdFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveEventTypeFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveEventTypeVersionFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveSourceFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveSubjectFilterConfigurations))]
        [TestCaseSource(nameof(GetPositiveTopicFilterConfigurations))]
        public void TestAdvancedFilteringSuccess(AdvancedFilterSetting filter)
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new AdvancedFilterSetting[] { filter } };
            Assert.True(filterConfig.AcceptsEvent(_gridEvent));
        }

        [TestCaseSource(nameof(GetNegativeIdFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeDataVersionFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeEventDataFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeEventIdFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeEventTypeFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeEventTypeVersionFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeSourceFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeSubjectFilterConfigurations))]
        [TestCaseSource(nameof(GetNegativeTopicFilterConfigurations))]
        public void TestAdvancedFilteringFailure(AdvancedFilterSetting filter)
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new AdvancedFilterSetting[] { filter } };
            Assert.False(filterConfig.AcceptsEvent(_gridEvent));
        }
    }
}
