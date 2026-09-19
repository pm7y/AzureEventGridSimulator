using System.Collections;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

internal class NegativeFilterTestCaseContainer : IEnumerable<object[]>
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
        list.AddRange(
            GetNegativeEventTypeVersionFilterConfigurations().Select(c => new object[] { c })
        );
        return list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static AdvancedFilterSetting[] GetNegativeIdFilterConfigurations()
    {
        return
        [
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = [],
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = [string.Empty],
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["A"],
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["a"],
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = [],
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = [string.Empty],
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = new[] { "a" },
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = ["TEN"],
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { "b" },
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { string.Empty },
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = Array.Empty<object>(),
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { "B" },
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { "nomatch1", "nomatch2" }, // test multiple values - none should match
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = Array.Empty<string>(),
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = new[] { "notCorrect" },
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = new[] { "different", "not_found", "Another" },
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                Values = new[] { "different", "EventID", "Another" },
            },
            new AdvancedFilterSetting
            {
                Key = "Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                Values = new[] { "different", "EventId", "Another" },
            },
        ];
    }

    private static AdvancedFilterSetting[] GetNegativeTopicFilterConfigurations()
    {
        return
        [
            new AdvancedFilterSetting
            {
                Key = "Topic",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["HE"],
            },
            new AdvancedFilterSetting
            {
                Key = "Topic",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["he_"],
            },
            new AdvancedFilterSetting
            {
                Key = "Topic",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = ["everest"],
            },
            new AdvancedFilterSetting
            {
                Key = "Topic",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { "123" },
            },
            new AdvancedFilterSetting
            {
                Key = "Topic",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = new object[] { "_event_" },
            },
            new AdvancedFilterSetting
            {
                Key = "Topic",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                Values = new object[] { "THE_EVENT_TOPIC" },
            },
        ];
    }

    private static AdvancedFilterSetting[] GetNegativeSubjectFilterConfigurations()
    {
        return
        [
            new AdvancedFilterSetting
            {
                Key = "Subject",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["E"],
            },
            new AdvancedFilterSetting
            {
                Key = "Subject",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["able"],
            },
            new AdvancedFilterSetting
            {
                Key = "Subject",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = ["x"],
            },
            new AdvancedFilterSetting
            {
                Key = "Subject",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { "sub" },
            },
            new AdvancedFilterSetting
            {
                Key = "Subject",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = new object[] { "not_correct" },
            },
            new AdvancedFilterSetting
            {
                Key = "Subject",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                Values = new object[] { "theeventsubject" },
            },
        ];
    }

    private static AdvancedFilterSetting[] GetNegativeEventTypeFilterConfigurations()
    {
        return
        [
            new AdvancedFilterSetting
            {
                Key = "EventType",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["his"],
            },
            new AdvancedFilterSetting
            {
                Key = "EventType",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["hIs"],
            },
            new AdvancedFilterSetting
            {
                Key = "EventType",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = [".."],
            },
            new AdvancedFilterSetting
            {
                Key = "EventType",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { "EVENTTYPE" },
            },
            new AdvancedFilterSetting
            {
                Key = "EventType",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = Array.Empty<object>(),
            },
            new AdvancedFilterSetting
            {
                Key = "EventType",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                Values = new object[] { "Not-the-right-type", "this.is.a.test.event.type" },
            },
        ];
    }

    private static AdvancedFilterSetting[] GetNegativeDataVersionFilterConfigurations()
    {
        return
        [
            new AdvancedFilterSetting
            {
                Key = "DataVersion",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["a"],
            },
            new AdvancedFilterSetting
            {
                Key = "DataVersion",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                Values = ["_"],
            },
            new AdvancedFilterSetting
            {
                Key = "DataVersion",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                Values = new[] { "7" },
            },
            new AdvancedFilterSetting
            {
                Key = "DataVersion",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = new object[] { "5.0.1" },
            },
            new AdvancedFilterSetting
            {
                Key = "DataVersion",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                Values = new object[] { "5.0" },
            },
        ];
    }

    private static AdvancedFilterSetting[] GetNegativeEventDataFilterConfigurations()
    {
        return
        [
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan,
                Value = 2,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan,
                Value = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan,
                Value = 1,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberGreaterThanOrEquals,
                Value = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberGreaterThanOrEquals,
                Value = 1.01,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberGreaterThanOrEquals,
                Value = 5,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                Values = new object[] { 1.1, 2, 3.5, "stringValue", true },
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                Values = Array.Empty<object>(),
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                Values = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn,
                Values = new object[] { 0, 1, 2, 3.5, "stringValue", true },
            },
            // while the value is not in the array, the fact that the values in the array are not all parsable as numbers means the full evaluation cannot be completed and so by default we fail
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn,
                Values = new object[] { 0, 2, 3.5, "stringValue", true },
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan,
                Value = 1,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan,
                Value = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan,
                Value = 0.99999999,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberLessThanOrEquals,
                Value = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberLessThanOrEquals,
                Value = 0.9,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberLessThanOrEquals,
                Value = -1,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.IsTrue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.BoolEquals,
                Value = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.IsTrue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.BoolEquals,
                Value = false,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = [],
            },
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith,
                Values = ["String_Value"],
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan,
                Value = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan,
                Value = 0.12345,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberGreaterThanOrEquals,
                Value = null,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberGreaterThanOrEquals,
                Value = 0.123451,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                Values = new object[] { 0.123451 },
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan,
                Value = 0.12345,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberLessThanOrEquals,
                Value = 0.1234,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.DoubleValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn,
                Values = new object[] { 0.12345 },
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberMaxValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan,
                Value = ulong.MaxValue,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberMaxValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                Values = new object[] { long.MaxValue },
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberMaxValue",
                OperatorType = AdvancedFilterSetting
                    .AdvancedFilterOperatorType
                    .NumberLessThanOrEquals,
                Value = long.MaxValue,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberMaxValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan,
                Value = ulong.MaxValue,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberMaxValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn,
                Values = new object[] { ulong.MaxValue },
            },
            new AdvancedFilterSetting
            {
                Key = "Data.SubObject.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                Values = new object[] { "Testing", "does", "not", "exist" },
            },
            new AdvancedFilterSetting
            {
                Key = "Data.SubObject.Id",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                Values = new object[] { 10, 11, 12 },
            },
            // New operators: StringNotContains - should NOT match when value DOES contain a filter value
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotContains,
                Values = new[] { "String" }, // "StringValue" contains "String"
            },
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotContains,
                Values = new[] { "VALUE" }, // case insensitive, "StringValue" contains "VALUE"
            },
            // StringNotBeginsWith - should NOT match when value DOES begin with a filter value
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotBeginsWith,
                Values = new[] { "String" }, // "StringValue" begins with "String"
            },
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotBeginsWith,
                Values = new[] { "STRING" }, // case insensitive
            },
            // StringNotEndsWith - should NOT match when value DOES end with a filter value
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotEndsWith,
                Values = new[] { "Value" }, // "StringValue" ends with "Value"
            },
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotEndsWith,
                Values = new[] { "VALUE" }, // case insensitive
            },
            // NumberInRange - should NOT match when value is NOT within any range
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberInRange,
                Values = new object[] { new object[] { 5, 10 } }, // 1 is not within [5, 10]
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberInRange,
                Values = new object[] { new object[] { 2, 5 }, new object[] { 10, 20 } }, // 1 is not within either range
            },
            // NumberNotInRange - should NOT match when value IS within a range
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotInRange,
                Values = new object[] { new object[] { 0, 2 } }, // 1 IS within [0, 2]
            },
            // IsNullOrUndefined - should NOT match when key exists and has a non-null value
            new AdvancedFilterSetting
            {
                Key = "Data.Name",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.IsNullOrUndefined,
            },
            new AdvancedFilterSetting
            {
                Key = "Data.NumberValue",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.IsNullOrUndefined,
            },
            // IsNotNull - should NOT match when key does NOT exist
            new AdvancedFilterSetting
            {
                Key = "Data.NonExistentKey",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull,
            },
        ];
    }

    private static AdvancedFilterSetting[] GetNegativeEventIdFilterConfigurations()
    {
        // The simulator doesn't resolve "EventId" (the event id is read with "Id" for both
        // schemas), so the key is treated as missing, and the default operator
        // (NumberGreaterThan) doesn't match a missing key.
        return [new AdvancedFilterSetting { Key = "EventId" }];
    }

    private static AdvancedFilterSetting[] GetNegativeSourceFilterConfigurations()
    {
        // "Source" resolves to the event topic (it's the CloudEvents name for it), so a
        // StringNotIn that lists the topic doesn't match. If "Source" stopped resolving, the
        // key would be missing and StringNotIn would match instead.
        return
        [
            new AdvancedFilterSetting
            {
                Key = "Source",
                OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                Values = ["THE_EVENT_TOPIC"],
            },
        ];
    }

    private static AdvancedFilterSetting[] GetNegativeEventTypeVersionFilterConfigurations()
    {
        // The simulator doesn't resolve "EventTypeVersion" for either schema, so the key is
        // treated as missing, and the default operator (NumberGreaterThan) doesn't match a
        // missing key.
        return [new AdvancedFilterSetting { Key = "EventTypeVersion" }];
    }
}
