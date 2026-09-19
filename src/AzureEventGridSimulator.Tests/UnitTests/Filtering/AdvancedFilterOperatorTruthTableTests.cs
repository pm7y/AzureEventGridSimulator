using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Filtering;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;
using Op = AzureEventGridSimulator.Infrastructure.Settings.AdvancedFilterSetting.AdvancedFilterOperatorType;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

/// <summary>
///     Golden truth table: every advanced filter operator against every shape of event value,
///     evaluated through AcceptsEvent(SimulatorEvent), the overload used for delivery. It pins
///     today's outcomes, quirks included (a bool reads as the number 1, BoolEquals false matches
///     a null value, the negated string operators match any non-string value, and an operand
///     that can't be evaluated is a no-match even for the negated operators), so a change to
///     how operators are evaluated can't alter an outcome unnoticed.
/// </summary>
[Trait("Category", "unit")]
public class AdvancedFilterOperatorTruthTableTests
{
    public enum ValueShape
    {
        MissingKey,
        NullValue,
        Int,
        NumericString,
        NonNumericString,
        Bool,
        NumberArray,
        NumberArrayWithArrayFiltering,
        StringArray,
        StringArrayWithArrayFiltering,
    }

    private static readonly SimulatorEvent _simulatorEvent = SimulatorEvent.FromEventGridEvent(
        TestHelpers.CreateValidEventGridEvent(
            data: new
            {
                IntValue = 5,
                NumericString = "5",
                Text = "Hello",
                BoolValue = true,
                NullValue = (string?)null,
                Numbers = new[] { 1, 5 },
                Strings = new[] { "Hello", "World" },
            }
        )
    );

    // The operands are chosen so that each value shape could match: 5 for the number operators
    // (1, the value of true, for the "less than" ones), and a text operand plus "5" for the
    // string operators.
    private static readonly Dictionary<string, FilterCase> _cases = new(StringComparer.Ordinal)
    {
        ["NumberGreaterThan 4"] = new(Op.NumberGreaterThan, Value: 4),
        ["NumberGreaterThanOrEquals 5"] = new(Op.NumberGreaterThanOrEquals, Value: 5),
        ["NumberLessThan 2"] = new(Op.NumberLessThan, Value: 2),
        ["NumberLessThanOrEquals 1"] = new(Op.NumberLessThanOrEquals, Value: 1),
        ["NumberIn [5]"] = new(Op.NumberIn, Values: [5]),
        ["NumberNotIn [5]"] = new(Op.NumberNotIn, Values: [5]),
        ["NumberInRange [4, 6]"] = new(Op.NumberInRange, Values: [new object[] { 4, 6 }]),
        ["NumberNotInRange [4, 6]"] = new(Op.NumberNotInRange, Values: [new object[] { 4, 6 }]),
        ["BoolEquals true"] = new(Op.BoolEquals, Value: true),
        ["BoolEquals false"] = new(Op.BoolEquals, Value: false),
        ["StringContains [ell, 5]"] = new(Op.StringContains, Values: ["ell", "5"]),
        ["StringNotContains [ell, 5]"] = new(Op.StringNotContains, Values: ["ell", "5"]),
        ["StringBeginsWith [He, 5]"] = new(Op.StringBeginsWith, Values: ["He", "5"]),
        ["StringNotBeginsWith [He, 5]"] = new(Op.StringNotBeginsWith, Values: ["He", "5"]),
        ["StringEndsWith [lo, 5]"] = new(Op.StringEndsWith, Values: ["lo", "5"]),
        ["StringNotEndsWith [lo, 5]"] = new(Op.StringNotEndsWith, Values: ["lo", "5"]),
        ["StringIn [hello, 5, true]"] = new(Op.StringIn, Values: ["hello", "5", "true"]),
        ["StringNotIn [hello, 5, true]"] = new(Op.StringNotIn, Values: ["hello", "5", "true"]),
        ["IsNullOrUndefined"] = new(Op.IsNullOrUndefined),
        ["IsNotNull"] = new(Op.IsNotNull),
    };

    // The filter cases that accept each value shape. Every other case rejects it.
    private static readonly Dictionary<ValueShape, string[]> _acceptedBy = new()
    {
        [ValueShape.MissingKey] =
        [
            "NumberNotIn [5]",
            "NumberNotInRange [4, 6]",
            "StringNotIn [hello, 5, true]",
            "IsNullOrUndefined",
        ],
        [ValueShape.NullValue] =
        [
            "BoolEquals false",
            "StringNotContains [ell, 5]",
            "StringNotBeginsWith [He, 5]",
            "StringNotEndsWith [lo, 5]",
            "StringNotIn [hello, 5, true]",
            "IsNullOrUndefined",
        ],
        [ValueShape.Int] =
        [
            "NumberGreaterThan 4",
            "NumberGreaterThanOrEquals 5",
            "NumberIn [5]",
            "NumberInRange [4, 6]",
            "BoolEquals true",
            "StringNotContains [ell, 5]",
            "StringNotBeginsWith [He, 5]",
            "StringNotEndsWith [lo, 5]",
            "StringIn [hello, 5, true]",
            "IsNotNull",
        ],
        [ValueShape.NumericString] =
        [
            "NumberGreaterThan 4",
            "NumberGreaterThanOrEquals 5",
            "NumberIn [5]",
            "NumberInRange [4, 6]",
            "StringContains [ell, 5]",
            "StringBeginsWith [He, 5]",
            "StringEndsWith [lo, 5]",
            "StringIn [hello, 5, true]",
            "IsNotNull",
        ],
        [ValueShape.NonNumericString] =
        [
            "StringContains [ell, 5]",
            "StringBeginsWith [He, 5]",
            "StringEndsWith [lo, 5]",
            "StringIn [hello, 5, true]",
            "IsNotNull",
        ],
        [ValueShape.Bool] =
        [
            "NumberLessThan 2",
            "NumberLessThanOrEquals 1",
            "NumberNotIn [5]",
            "NumberNotInRange [4, 6]",
            "BoolEquals true",
            "StringNotContains [ell, 5]",
            "StringNotBeginsWith [He, 5]",
            "StringNotEndsWith [lo, 5]",
            "StringIn [hello, 5, true]",
            "IsNotNull",
        ],
        // Without array filtering the array is one value that is neither a number nor a string
        [ValueShape.NumberArray] =
        [
            "StringNotContains [ell, 5]",
            "StringNotBeginsWith [He, 5]",
            "StringNotEndsWith [lo, 5]",
            "StringNotIn [hello, 5, true]",
            "IsNotNull",
        ],
        // With array filtering, positive operators need any element [1, 5] to match and
        // negated operators need every element to match
        [ValueShape.NumberArrayWithArrayFiltering] =
        [
            "NumberGreaterThan 4",
            "NumberGreaterThanOrEquals 5",
            "NumberLessThan 2",
            "NumberLessThanOrEquals 1",
            "NumberIn [5]",
            "NumberInRange [4, 6]",
            "BoolEquals true",
            "StringNotContains [ell, 5]",
            "StringNotBeginsWith [He, 5]",
            "StringNotEndsWith [lo, 5]",
            "StringIn [hello, 5, true]",
            "IsNotNull",
        ],
        [ValueShape.StringArray] =
        [
            "StringNotContains [ell, 5]",
            "StringNotBeginsWith [He, 5]",
            "StringNotEndsWith [lo, 5]",
            "StringNotIn [hello, 5, true]",
            "IsNotNull",
        ],
        // Elements are "Hello" (matches every string operand) and "World" (matches none)
        [ValueShape.StringArrayWithArrayFiltering] =
        [
            "StringContains [ell, 5]",
            "StringBeginsWith [He, 5]",
            "StringEndsWith [lo, 5]",
            "StringIn [hello, 5, true]",
            "IsNotNull",
        ],
    };

    public static TheoryData<string, ValueShape, bool> TruthTable()
    {
        var data = new TheoryData<string, ValueShape, bool>();
        foreach (var filterCase in _cases.Keys)
        {
            foreach (var shape in Enum.GetValues<ValueShape>())
            {
                data.Add(
                    filterCase,
                    shape,
                    _acceptedBy[shape].Contains(filterCase, StringComparer.Ordinal)
                );
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TruthTable))]
    public void GivenOperatorAndValueShape_WhenFilterEvaluated_ThenOutcomeMatchesTruthTable(
        string filterCase,
        ValueShape shape,
        bool expected
    )
    {
        var (key, enableArrayFiltering) = shape switch
        {
            ValueShape.MissingKey => ("Data.MissingValue", false),
            ValueShape.NullValue => ("Data.NullValue", false),
            ValueShape.Int => ("Data.IntValue", false),
            ValueShape.NumericString => ("Data.NumericString", false),
            ValueShape.NonNumericString => ("Data.Text", false),
            ValueShape.Bool => ("Data.BoolValue", false),
            ValueShape.NumberArray => ("Data.Numbers", false),
            ValueShape.NumberArrayWithArrayFiltering => ("Data.Numbers", true),
            ValueShape.StringArray => ("Data.Strings", false),
            ValueShape.StringArrayWithArrayFiltering => ("Data.Strings", true),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };
        var spec = _cases[filterCase];
        var filterConfig = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = enableArrayFiltering,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = key,
                    OperatorType = spec.Operator,
                    Value = spec.Value,
                    Values = spec.Values,
                },
            ],
        };

        filterConfig.AcceptsEvent(_simulatorEvent).ShouldBe(expected, $"{filterCase} / {shape}");
    }

    [Fact]
    public void GivenTruthTable_WhenInspected_ThenEveryOperatorIsCoveredAndEveryNameIsKnown()
    {
        Enum.GetValues<Op>().Except(_cases.Values.Select(c => c.Operator)).ShouldBeEmpty();
        _acceptedBy
            .Values.SelectMany(names => names)
            .Except(_cases.Keys, StringComparer.Ordinal)
            .ShouldBeEmpty();
        Enum.GetValues<ValueShape>().Except(_acceptedBy.Keys).ShouldBeEmpty();
    }

    // Values bound with System.Text.Json (as SubscribersSettingsConverter does) arrive as
    // JsonElement rather than string; the string operators still compare them as strings
    [Theory]
    [InlineData(Op.StringContains, """["ell"]""", true)]
    [InlineData(Op.StringNotContains, """["ell"]""", false)]
    [InlineData(Op.StringBeginsWith, """["He"]""", true)]
    [InlineData(Op.StringNotBeginsWith, """["He"]""", false)]
    [InlineData(Op.StringEndsWith, """["lo"]""", true)]
    [InlineData(Op.StringNotEndsWith, """["lo"]""", false)]
    [InlineData(Op.StringIn, """["hello"]""", true)]
    [InlineData(Op.StringNotIn, """["hello"]""", false)]
    public void GivenStringOperandsDeserializedFromJson_WhenFilterEvaluated_ThenOperandsCompareAsStrings(
        Op operatorType,
        string valuesJson,
        bool expected
    )
    {
        var values = JsonSerializer.Deserialize<List<object>>(valuesJson) ?? [];
        values.ShouldAllBe(v => v is JsonElement);
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Text",
                    OperatorType = operatorType,
                    Values = values,
                },
            ],
        };

        filterConfig.AcceptsEvent(_simulatorEvent).ShouldBe(expected, operatorType.ToString());
    }

    public static TheoryData<Op> AllOperators()
    {
        var data = new TheoryData<Op>();
        foreach (var operatorType in Enum.GetValues<Op>())
        {
            data.Add(operatorType);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllOperators))]
    public void GivenOperatorWithNoOperand_WhenValidated_ThenOnlyNullCheckOperatorsAreValid(
        Op operatorType
    )
    {
        var filter = new AdvancedFilterSetting
        {
            Key = "Data.IntValue",
            OperatorType = operatorType,
        };

        if (operatorType == Op.IsNullOrUndefined || operatorType == Op.IsNotNull)
        {
            Should.NotThrow(filter.Validate);
        }
        else
        {
            Should
                .Throw<ArgumentException>(filter.Validate)
                .Message.ShouldBe(
                    "Either a Value or a set of Values must be provided (Parameter 'Value')"
                );
        }
    }

    [Theory]
    [MemberData(nameof(AllOperators))]
    public void GivenOperatorWithValueButNoValues_WhenValidated_ThenOnlyRangeOperatorsAreInvalid(
        Op operatorType
    )
    {
        var filter = new AdvancedFilterSetting
        {
            Key = "Data.IntValue",
            OperatorType = operatorType,
            Value = 5,
        };

        if (operatorType == Op.NumberInRange || operatorType == Op.NumberNotInRange)
        {
            Should
                .Throw<ArgumentException>(filter.Validate)
                .Message.ShouldBe(
                    "NumberInRange and NumberNotInRange operators require at least one range specified as [min, max] pairs in Values (Parameter 'Values')"
                );
        }
        else
        {
            Should.NotThrow(filter.Validate);
        }
    }

    private sealed record FilterCase(Op Operator, object? Value = null, object[]? Values = null);
}
