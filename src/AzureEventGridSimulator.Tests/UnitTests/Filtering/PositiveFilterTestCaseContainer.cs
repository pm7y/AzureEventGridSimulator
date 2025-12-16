using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

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

    private static AdvancedFilterSetting[] GetPositiveIdFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "E" }},
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "Event" }},
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "Event" }},
            new AdvancedFilterSetting
            {
                Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "EVE" }
            }, // according to the spec, string comparisons in advanced mode are always case insensitive
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "E" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "ent" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "ENT"} },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "d"} },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "EventId"} },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "EventId" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "Id" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "d" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "D" },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new[] { "EventId" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new[] { "eventid" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new[] { "EVENTID" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new[] { "different", "EVENTID", "Another" } },
            new AdvancedFilterSetting
                { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new object[] { "different", "EVENTID", "Another" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn, Values = new[] { "different", "notfound", "Another" } },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn, Values = null },
            new AdvancedFilterSetting { Key = "Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn, Values = Array.Empty<string>() }
        };
    }

    private static AdvancedFilterSetting[] GetPositiveTopicFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "THE" }},
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "the_" }},
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "event" } },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "Ic" },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new object[] { "the_event_topic" } },
            new AdvancedFilterSetting { Key = "Topic", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn, Values = new object[] { "not_the_right_one" } }
        };
    }

    private static AdvancedFilterSetting[] GetPositiveSubjectFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "THE" }},
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "theE" }},
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "event" } },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "Subject" },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new object[] { "theeventsubject" } },
            new AdvancedFilterSetting { Key = "Subject", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn, Values = new object[] { "NotTheEventSubject" } }
        };
    }

    private static AdvancedFilterSetting[] GetPositiveEventTypeFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "this" }},
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "ThIs" }},
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { ".event." }},
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "EVENT.TYPE" },
            new AdvancedFilterSetting
                { Key = "EventType", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new object[] { "this.is.a.test.event.type" } },
            new AdvancedFilterSetting { Key = "EventType", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn, Values = new object[] { "Not-the-right-type" } }
        };
    }

    private static AdvancedFilterSetting[] GetPositiveDataVersionFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "5" }},
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains, Values = new[] { "." }},
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith, Value = "0" },
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new object[] { "5.0" } },
            new AdvancedFilterSetting { Key = "DataVersion", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn, Values = new object[] { "5" } }
        };
    }

    private static AdvancedFilterSetting[] GetPositiveEventDataFilterConfigurations()
    {
        return new[]
        {
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan, Value = 0 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan, Value = 0.5 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThanOrEquals, Value = 0.5 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThanOrEquals, Value = 1 },
            new AdvancedFilterSetting
                { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn, Values = new object[] { 1.0, 2, 3.5, "stringValue", true } },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn, Values = Array.Empty<object>() },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn, Values = null },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan, Value = 1.1 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan, Value = 2 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThanOrEquals, Value = 2 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThanOrEquals, Value = 2 },
            new AdvancedFilterSetting { Key = "Data.NumberValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThanOrEquals, Value = 1 },
            new AdvancedFilterSetting { Key = "Data.IsTrue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.BoolEquals, Value = true },
            new AdvancedFilterSetting { Key = "Data.Name", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith, Values = new[] { "StringValue" }},
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan, Value = 0.123449 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThanOrEquals, Value = 0.12345 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn, Values = new object[] { 0.12345 } },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan, Value = 0.123451 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThanOrEquals, Value = 0.12345 },
            new AdvancedFilterSetting { Key = "Data.DoubleValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn, Values = new object[] { 0.123451 } },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan, Value = long.MaxValue },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThanOrEquals, Value = ulong.MaxValue },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn, Values = new object[] { ulong.MaxValue } },
            new AdvancedFilterSetting { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThanOrEquals, Value = ulong.MaxValue },
            new AdvancedFilterSetting
                { Key = "Data.NumberMaxValue", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn, Values = new object[] { long.MaxValue } },
            new AdvancedFilterSetting { Key = "Data.SubObject.Name", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn, Values = new object[] { "Test" } },
            new AdvancedFilterSetting { Key = "Data.SubObject.Id", OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn, Values = new object[] { 1 } }
        };
    }
}
