using System.Text.Json.Serialization;
using AzureEventGridSimulator.Infrastructure.Extensions;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class AdvancedFilterSetting
{
    public enum AdvancedFilterOperatorType
    {
        // Number operators
        NumberGreaterThan,
        NumberGreaterThanOrEquals,
        NumberLessThan,
        NumberLessThanOrEquals,
        NumberIn,
        NumberNotIn,
        NumberInRange,
        NumberNotInRange,

        // Boolean operator
        BoolEquals,

        // String operators
        StringContains,
        StringNotContains,
        StringBeginsWith,
        StringNotBeginsWith,
        StringEndsWith,
        StringNotEndsWith,
        StringIn,
        StringNotIn,

        // Null check operators
        IsNullOrUndefined,
        IsNotNull,
    }

    private static readonly string[] values = ["null"];

    [JsonPropertyName("operatorType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AdvancedFilterOperatorType OperatorType { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("value")]
    public object? Value { get; init; }

    [JsonPropertyName("values")]
    public ICollection<object>? Values { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new ArgumentException("A filter key must be provided", nameof(Key));
        }

        // IsNullOrUndefined and IsNotNull don't require values
        var nullCheckOperators = new[]
        {
            AdvancedFilterOperatorType.IsNullOrUndefined,
            AdvancedFilterOperatorType.IsNotNull,
        };

        if (!nullCheckOperators.Contains(OperatorType) && Value == null && !Values.HasItems())
        {
            throw new ArgumentException(
                "Either a Value or a set of Values must be provided",
                nameof(Value)
            );
        }

        const short maxStringLength = 512;

        if ((Value as string)?.Length > maxStringLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Value),
                $"Advanced filtering limits strings to {maxStringLength} characters per string value"
            );
        }

        if (Values?.Any(o => (o as string)?.Length > maxStringLength) == true)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Values),
                $"Advanced filtering limits strings to {maxStringLength} characters per string value"
            );
        }

        // In/NotIn operators are limited to 5 values
        if (
            new[]
            {
                AdvancedFilterOperatorType.NumberIn,
                AdvancedFilterOperatorType.NumberNotIn,
                AdvancedFilterOperatorType.StringIn,
                AdvancedFilterOperatorType.StringNotIn,
            }.Contains(OperatorType)
            && Values?.Count > 5
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(OperatorType),
                "Advanced filtering limits filters to five values for in and not in operators"
            );
        }

        // Range operators require values to be provided in pairs (min, max)
        if (
            new[]
            {
                AdvancedFilterOperatorType.NumberInRange,
                AdvancedFilterOperatorType.NumberNotInRange,
            }.Contains(OperatorType)
        )
        {
            if (Values == null || Values.Count == 0)
            {
                throw new ArgumentException(
                    "NumberInRange and NumberNotInRange operators require at least one range specified as [min, max] pairs in Values",
                    nameof(Values)
                );
            }
        }
    }

    public override string ToString()
    {
        return string.Join(
            ", ",
            Key,
            OperatorType,
            Value ?? "null",
            string.Join(", ", Values?.Select(v => v.ToString()) ?? values),
            Guid.NewGuid()
        );
    }
}
