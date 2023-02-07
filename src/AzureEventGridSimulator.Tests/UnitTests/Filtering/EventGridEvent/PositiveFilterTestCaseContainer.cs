namespace AzureEventGridSimulator.Tests.UnitTests.Filtering.EventGridEvent;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Infrastructure.Settings;

internal class PositiveFilterTestCaseContainer : IEnumerable<object[]>
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

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static IEnumerable<AdvancedFilterSetting> GetPositiveIdFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "E" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "Event" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "Event" },
            new AdvancedFilterSetting
            {
                Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "EVE"
            }, // according to the spec, string comparisons in advanced mode are always case insensitive
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "E" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "ent" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "ENT" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "d" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "EventId" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "EventId" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "Id" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "d" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "D" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new[] { "EventId" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new[] { "eventid" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new[] { "EVENTID" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new[] { "different", "EVENTID", "Another" } },
            new AdvancedFilterSetting
                { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new object[] { "different", "EVENTID", "Another" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringNotIn, Values = new[] { "different", "notfound", "Another" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringNotIn, Values = null },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringNotIn, Values = Array.Empty<string>() }
        };
    }

    private static IEnumerable<AdvancedFilterSetting> GetPositiveTopicFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "THE" },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "the_" },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "event" },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "Ic" },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new object[] { "the_event_topic" } },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringNotIn, Values = new object[] { "not_the_right_one" } }
        };
    }

    private static IEnumerable<AdvancedFilterSetting> GetPositiveSubjectFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "THE" },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "theE" },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "event" },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "Subject" },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new object[] { "theeventsubject" } },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringNotIn, Values = new object[] { "NotTheEventSubject" } }
        };
    }

    private static IEnumerable<AdvancedFilterSetting> GetPositiveEventTypeFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "this" },
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "ThIs" },
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = ".event." },
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "EVENT.TYPE" },
            new AdvancedFilterSetting
                { Key = "EventType", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new object[] { "this.is.a.test.event.type" } },
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringNotIn, Values = new object[] { "Not-the-right-type" } }
        };
    }

    private static IEnumerable<AdvancedFilterSetting> GetPositiveDataVersionFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "5" },
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringContains, Value = "." },
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith, Value = "0" },
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new object[] { "5.0" } },
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringNotIn, Values = new object[] { "5" } }
        };
    }

    private static IEnumerable<AdvancedFilterSetting> GetPositiveEventDataFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThan, Value = 0 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThan, Value = 0.5 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 0.5 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 1 },
            new AdvancedFilterSetting
                { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberIn, Values = new object[] { 1.0, 2, 3.5, "stringValue", true } },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn, Values = Array.Empty<object>() },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn, Values = null },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThan, Value = 1.1 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThan, Value = 2 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThanOrEquals, Value = 2 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThanOrEquals, Value = 2 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThanOrEquals, Value = 1 },
            new AdvancedFilterSetting { Key = "Data.IsTrue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.BoolEquals, Value = true },
            new AdvancedFilterSetting { Key = "Data.Name", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith, Value = "StringValue" },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThan, Value = 0.123449 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 0.12345 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberIn, Values = new object[] { 0.12345 } },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThan, Value = 0.123451 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThanOrEquals, Value = 0.12345 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn, Values = new object[] { 0.123451 } },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThan, Value = long.MaxValue },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThanOrEquals, Value = ulong.MaxValue },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberIn, Values = new object[] { ulong.MaxValue } },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThanOrEquals, Value = ulong.MaxValue },
            new AdvancedFilterSetting
                { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn, Values = new object[] { long.MaxValue } },
            new AdvancedFilterSetting { Key = "Data.SubObject.Name", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.StringIn, Values = new object[] { "Test" } },
            new AdvancedFilterSetting { Key = "Data.SubObject.Id", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberIn, Values = new object[] { 1 } }
        };
    }
}
