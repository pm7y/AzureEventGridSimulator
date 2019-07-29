using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Settings;
using static AzureEventGridSimulator.Settings.AdvancedFilterSetting;

namespace Tests
{
    class PositiveFilterTestCaseContainer : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            var list = new List<object[]>();
            list.AddRange(GetPositiveIdFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetPositiveTopicFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetPositiveSubjectFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetPositiveEventTypeFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetPositiveDataVersionFilterConfigurations().Select(c => new object[] { c }));
            list.AddRange(GetPositiveEventDataFilterConfigurations().Select(c => new object[] { c }));
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static AdvancedFilterSetting[] GetPositiveIdFilterConfigurations()
        {
            return new AdvancedFilterSetting[]
            {
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
                new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object [0] },
                new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = null },
                new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 1.1 },
                new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 2 },
                new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 2 },
                new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 2 },
                new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 1 },
                new AdvancedFilterSetting { Key = "Data.IsTrue", OperatorType = OperatorTypeEnum.BoolEquals, Value = true },
                new AdvancedFilterSetting { Key = "Data.Name", OperatorType = OperatorTypeEnum.StringBeginsWith, Value = "StringValue"},
                new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = 0.123449},
                new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 0.12345},
                new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object[] { 0.12345 } },
                new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThan, Value = 0.123451 },
                new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = 0.12345 },
                new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object[] { 0.123451 } },
                new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberGreaterThan, Value = long.MaxValue },
                new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberGreaterThanOrEquals, Value = ulong.MaxValue },
                new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberIn, Values = new object[] { ulong.MaxValue } },
                new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberLessThanOrEquals, Value = ulong.MaxValue },
                new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = OperatorTypeEnum.NumberNotIn, Values = new object[] { long.MaxValue } },
            };
        }
    }
}
