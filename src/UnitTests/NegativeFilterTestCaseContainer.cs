using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Settings;
using static AzureEventGridSimulator.Settings.AdvancedFilterSetting;

namespace Tests
{
    class NegativeFilterTestCaseContainer : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            var list = new List<object[]>();
            list.AddRange(GetNegativeIdFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeTopicFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeSubjectFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeEventTypeFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeDataVersionFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeEventDataFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeEventIdFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeSourceFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetNegativeEventTypeVersionFilterConfigurations().Select(c => new object[] { c }));
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static AdvancedFilterSetting[] GetNegativeIdFilterConfigurations()
        {
            return new AdvancedFilterSetting[]
            {
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = string.Empty },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "A" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "a" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = string.Empty },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "a" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringContains, Value = "TEN" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "b" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = string.Empty },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringEndsWith, Value = "B" },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = null },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[0] },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"notCorrect" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringIn, Values = new string[] {"different", "not_found", "Another" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringNotIn, Values = new string[] {"different", "EventID", "Another" } },
                new AdvancedFilterSetting { Key = "Id", OperatorType = OperatorTypeEnum.StringNotIn, Values = new string[] {"different", "EventId", "Another" } },
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
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringBeginsWith, Value ="a" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringContains, Value ="_" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringEndsWith, Value ="7" },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringIn, Values = new object[] { "5.0.1" } },
               new AdvancedFilterSetting { Key = "DataVersion", OperatorType = OperatorTypeEnum.StringNotIn, Values = new object[] { "5.0" } }
                          };
        }

        private static AdvancedFilterSetting[] GetNegativeEventDataFilterConfigurations()
        {
            return new AdvancedFilterSetting[] {
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 2 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = null },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 1 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = null},
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 1.01 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 5 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object []{ 1.1, 2, 3.5, "stringValue", true } },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object [0] },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberIn, Values = null },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object []{ 0, 1, 2, 3.5, "stringValue", true } },
               // while the value is not in the array, the fact that the values in the array are not all parsable as numbers means the full evaluation cannot be completed and so by default we fail
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object []{ 0, 2, 3.5, "stringValue", true } },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 1 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = null },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 0.99999999 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = null },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 0.9 },
               new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = -1 },
               new AdvancedFilterSetting { Key = "Data.IsTrue", OperatorType = OperatorTypeEnum.BoolEquals, Value = null },
               new AdvancedFilterSetting { Key = "Data.IsTrue", OperatorType = OperatorTypeEnum.BoolEquals, Value = false },
               new AdvancedFilterSetting { Key = "Data.Name", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = null },
               new AdvancedFilterSetting { Key = "Data.Name", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "String_Value" },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = null },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 0.12345},
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = null },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 0.123451 },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object[] { 0.123451 } },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 0.12345 },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 0.1234 },
               new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object[] { 0.12345 } },
               new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = ulong.MaxValue },
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
    }
}
