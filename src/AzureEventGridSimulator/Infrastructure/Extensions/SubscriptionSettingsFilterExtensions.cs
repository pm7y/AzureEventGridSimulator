using System.Globalization;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class SubscriptionSettingsFilterExtensions
{
    private static bool IsNegationOperator(
        AdvancedFilterSetting.AdvancedFilterOperatorType operatorType
    )
    {
        return operatorType
            is AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn
                or AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotInRange
                or AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn
                or AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotContains
                or AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotBeginsWith
                or AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotEndsWith;
    }

    /// <summary>
    ///     Per Azure docs, when the filter key is missing from the event only NumberNotIn and
    ///     StringNotIn evaluate as matched; the other negation operators (StringNotContains,
    ///     StringNotBeginsWith, StringNotEndsWith) evaluate as not matched.
    ///     NumberNotInRange is undocumented; it follows the NumberNotIn behaviour.
    /// </summary>
    private static bool MatchesWhenKeyMissing(
        AdvancedFilterSetting.AdvancedFilterOperatorType operatorType
    )
    {
        return operatorType
            is AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn
                or AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotInRange
                or AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn;
    }

    private static bool EvaluateAdvancedFilter(AdvancedFilterSetting filter, object? value)
    {
        bool retVal;

        switch (filter.OperatorType)
        {
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan:
                retVal = Try(() => value.ToNumber() > filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThanOrEquals:
                retVal = Try(() => value.ToNumber() >= filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan:
                retVal = Try(() => value.ToNumber() < filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThanOrEquals:
                retVal = Try(() => value.ToNumber() <= filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn:
                retVal = Try(() =>
                    (filter.Values ?? Array.Empty<object>())
                        .Select(v => v.ToNumber())
                        .Contains(value.ToNumber())
                );
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn:
                retVal = Try(() =>
                    !(filter.Values ?? Array.Empty<object>())
                        .Select(v => v.ToNumber())
                        .Contains(value.ToNumber())
                );
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.BoolEquals:
                retVal = Try(() => Convert.ToBoolean(value) == Convert.ToBoolean(filter.Value));
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains:
                {
                    var valueAsString = value as string;
                    retVal = Try(() =>
                        !string.IsNullOrEmpty(valueAsString)
                        && (filter.Values ?? Array.Empty<object>())
                            .Select(Convert.ToString)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Any(filterValue =>
                                valueAsString.Contains(
                                    filterValue!,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                    );
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith:
                {
                    // null or empty values cannot be considered to be the start character of a string
                    var valueAsString = value as string;
                    retVal = Try(() =>
                        !string.IsNullOrEmpty(valueAsString)
                        && (filter.Values ?? Array.Empty<object>())
                            .Select(Convert.ToString)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Any(filterValue =>
                                valueAsString.StartsWith(
                                    filterValue!,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                    );
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith:
                {
                    // null or empty values cannot be considered to be the end character of a string
                    var valueAsString = value as string;
                    retVal = Try(() =>
                        !string.IsNullOrEmpty(valueAsString)
                        && (filter.Values ?? Array.Empty<object>())
                            .Select(Convert.ToString)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Any(filterValue =>
                                valueAsString.EndsWith(
                                    filterValue!,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                    );
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn:
                retVal = Try(() =>
                    (filter.Values ?? Array.Empty<object>())
                        .Select(v => Convert.ToString(v)?.ToUpperInvariant())
                        .Contains(Convert.ToString(value)?.ToUpperInvariant())
                );
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn:
                retVal = Try(() =>
                    !(filter.Values ?? Array.Empty<object>())
                        .Select(v => Convert.ToString(v)?.ToUpperInvariant())
                        .Contains(Convert.ToString(value)?.ToUpperInvariant())
                );
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberInRange:
                retVal = Try(() => IsNumberInRanges(value.ToNumber(), filter.Values));
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotInRange:
                retVal = Try(() => !IsNumberInRanges(value.ToNumber(), filter.Values));
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotContains:
                {
                    var valueAsString = value as string;
                    retVal = Try(() =>
                        string.IsNullOrEmpty(valueAsString)
                        || !(filter.Values ?? Array.Empty<object>())
                            .Select(Convert.ToString)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Any(filterValue =>
                                valueAsString.Contains(
                                    filterValue!,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                    );
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotBeginsWith:
                {
                    var valueAsString = value as string;
                    retVal = Try(() =>
                        string.IsNullOrEmpty(valueAsString)
                        || !(filter.Values ?? Array.Empty<object>())
                            .Select(Convert.ToString)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Any(filterValue =>
                                valueAsString.StartsWith(
                                    filterValue!,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                    );
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotEndsWith:
                {
                    var valueAsString = value as string;
                    retVal = Try(() =>
                        string.IsNullOrEmpty(valueAsString)
                        || !(filter.Values ?? Array.Empty<object>())
                            .Select(Convert.ToString)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Any(filterValue =>
                                valueAsString.EndsWith(
                                    filterValue!,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                    );
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.IsNullOrUndefined:
            case AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull:
                // These are handled in AcceptsEvent before calling EvaluateAdvancedFilter
                // If we get here, the key exists and has a non-null value
                retVal =
                    filter.OperatorType
                    == AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(AdvancedFilterSetting.AdvancedFilterOperatorType),
                    "Unknown filter operator"
                );
        }

        return retVal;
    }

    private static bool TryGetValue(
        this SimulatorEvent simulatorEvent,
        string? key,
        out object? value
    )
    {
        value = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        // Map common property names to SimulatorEvent accessors
        switch (key)
        {
            case "Id":
            case "id":
                value = simulatorEvent.Id;
                return true;
            case "Topic":
            case "topic":
            case "Source":
            case "source":
                value = simulatorEvent.Source;
                return true;
            case "Subject":
            case "subject":
                value = simulatorEvent.Subject;
                return true;
            case "EventType":
            case "eventType":
            case "Type":
            case "type":
                value = simulatorEvent.EventType;
                return true;
            case "DataVersion":
            case "dataVersion":
            case "DataSchema":
            case "dataschema":
                value = simulatorEvent.DataVersion;
                return true;
            case "Data":
            case "data":
                value = simulatorEvent.Data;
                return true;
            default:
                // Handle nested data properties (e.g., "Data.propertyName")
                var split = key.Split('.');
                if (
                    string.Equals(split[0], "Data", StringComparison.OrdinalIgnoreCase)
                    && simulatorEvent.Data != null
                    && split.Length > 1
                )
                {
                    if (TryGetNestedValue(simulatorEvent.Data, split, 1, out var nestedValue))
                    {
                        value = nestedValue;
                        return true;
                    }
                }

                return false;
        }
    }

    private static bool TryGetNestedValue(
        object data,
        string[] pathParts,
        int startIndex,
        out object? value
    )
    {
        value = null;
        try
        {
            var json = JsonSerializer.Serialize(data);
            using var document = JsonDocument.Parse(json);
            var current = document.RootElement;

            for (var i = startIndex; i < pathParts.Length; i++)
            {
                if (current.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!current.TryGetProperty(pathParts[i], out var property))
                {
                    // Try case-insensitive match
                    var found = false;
                    foreach (var prop in current.EnumerateObject())
                    {
                        if (prop.Name.Equals(pathParts[i], StringComparison.OrdinalIgnoreCase))
                        {
                            current = prop.Value;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }
                else
                {
                    current = property;
                }
            }

            value = ConvertJsonElement(current);
            return value != null || current.ValueKind == JsonValueKind.Null;
        }
        catch
        {
            return false;
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => ConvertJsonArray(element),
            _ => element.GetRawText(),
        };
    }

    private static List<object?> ConvertJsonArray(JsonElement arrayElement)
    {
        var result = new List<object?>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            result.Add(ConvertJsonElement(item));
        }

        return result;
    }

    /// <summary>
    ///     Attempts to extract array elements from a value for array filtering.
    ///     Returns null if the value is not an array.
    /// </summary>
    private static IEnumerable<object?>? TryGetArrayElements(object? value)
    {
        return value switch
        {
            List<object?> list => list,
            IEnumerable<object> enumerable => enumerable.Cast<object?>(),
            JsonElement { ValueKind: JsonValueKind.Array } jsonArray => ConvertJsonArray(jsonArray),
            _ => null,
        };
    }

    /// <summary>
    ///     Evaluates an advanced filter against a value, with optional array filtering support.
    ///     When enableArrayFiltering is true and the value is an array, returns true if ANY element matches.
    /// </summary>
    private static bool EvaluateWithArraySupport(
        AdvancedFilterSetting filter,
        object? value,
        bool enableArrayFiltering
    )
    {
        if (!enableArrayFiltering)
        {
            return EvaluateAdvancedFilter(filter, value);
        }

        // Check if the value is an array
        var arrayElements = TryGetArrayElements(value);
        if (arrayElements == null)
        {
            // Not an array, evaluate normally
            return EvaluateAdvancedFilter(filter, value);
        }

        // For negation operators on arrays, ALL elements must satisfy the condition
        if (IsNegationOperator(filter.OperatorType))
        {
            return arrayElements.All(element => EvaluateAdvancedFilter(filter, element));
        }

        // For positive operators on arrays, ANY element must satisfy the condition
        return arrayElements.Any(element => EvaluateAdvancedFilter(filter, element));
    }

    private static double ToNumber(this object? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(
                nameof(value),
                "null is not convertible to a number in this implementation"
            );
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static bool Try(Func<bool> function, bool valueOnException = false)
    {
        try
        {
            return function();
        }
        catch
        {
            return valueOnException;
        }
    }

    /// <summary>
    ///     Checks if a number is within any of the specified ranges.
    ///     Ranges are specified as arrays like [[min1, max1], [min2, max2]] in the Values collection.
    /// </summary>
    private static bool IsNumberInRanges(double value, ICollection<object>? ranges)
    {
        if (ranges == null || ranges.Count == 0)
        {
            return false;
        }

        foreach (var range in ranges)
        {
            double min,
                max;

            // Handle JsonElement (from System.Text.Json deserialization)
            if (range is JsonElement { ValueKind: JsonValueKind.Array } jsonElement)
            {
                var length = jsonElement.GetArrayLength();
                if (length >= 2)
                {
                    min = jsonElement[0].GetDouble();
                    max = jsonElement[1].GetDouble();
                }
                else
                {
                    continue;
                }
            }
            // Handle object array
            else if (range is object[] { Length: >= 2 } objArray)
            {
                min = Convert.ToDouble(objArray[0], CultureInfo.InvariantCulture);
                max = Convert.ToDouble(objArray[1], CultureInfo.InvariantCulture);
            }
            // Handle IList<object>
            else if (range is IList<object> { Count: >= 2 } list)
            {
                min = Convert.ToDouble(list[0], CultureInfo.InvariantCulture);
                max = Convert.ToDouble(list[1], CultureInfo.InvariantCulture);
            }
            else
            {
                // Skip invalid range format
                continue;
            }

            // Check if value is within this range (inclusive)
            if (value >= min && value <= max)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetValue(this EventGridEvent gridEvent, string? key, out object? value)
    {
        var retval = false;
        value = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            return retval;
        }

        // Azure filter keys are case-insensitive (e.g. "id", "eventType", "data.key1")
        switch (key)
        {
            case var _ when key.Equals(nameof(gridEvent.Id), StringComparison.OrdinalIgnoreCase):
                value = gridEvent.Id;
                retval = true;
                break;
            case var _ when key.Equals(nameof(gridEvent.Topic), StringComparison.OrdinalIgnoreCase):
                value = gridEvent.Topic;
                retval = true;
                break;
            case var _
                when key.Equals(nameof(gridEvent.Subject), StringComparison.OrdinalIgnoreCase):
                value = gridEvent.Subject;
                retval = true;
                break;
            case var _
                when key.Equals(nameof(gridEvent.EventType), StringComparison.OrdinalIgnoreCase):
                value = gridEvent.EventType;
                retval = true;
                break;
            case var _
                when key.Equals(nameof(gridEvent.DataVersion), StringComparison.OrdinalIgnoreCase):
                value = gridEvent.DataVersion;
                retval = true;
                break;
            case var _ when key.Equals(nameof(gridEvent.Data), StringComparison.OrdinalIgnoreCase):
                value = gridEvent.Data;
                retval = true;
                break;
            default:
                var split = key.Split('.');
                if (
                    !string.Equals(
                        split[0],
                        nameof(gridEvent.Data),
                        StringComparison.OrdinalIgnoreCase
                    )
                    || gridEvent.Data == null
                    || split.Length <= 1
                )
                {
                    break;
                }

                if (TryGetNestedValue(gridEvent.Data, split, 1, out var nestedValue))
                {
                    retval = true;
                    value = nestedValue;
                }

                break;
        }

        return retval;
    }

    /// <summary>
    ///     Evaluates an advanced filter against a SimulatorEvent with array filtering support.
    /// </summary>
    private static bool AcceptsAdvancedFilter(
        AdvancedFilterSetting filter,
        SimulatorEvent simulatorEvent,
        bool enableArrayFiltering
    )
    {
        var keyExists = simulatorEvent.TryGetValue(filter.Key, out var value);
        var valueIsNull = keyExists && value == null;

        // Handle null check operators specially - they evaluate based on key existence
        switch (filter.OperatorType)
        {
            case AdvancedFilterSetting.AdvancedFilterOperatorType.IsNullOrUndefined:
                return !keyExists || valueIsNull;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull:
                return keyExists && !valueIsNull;
        }

        if (!keyExists)
        {
            return MatchesWhenKeyMissing(filter.OperatorType);
        }

        return EvaluateWithArraySupport(filter, value, enableArrayFiltering);
    }

    /// <summary>
    ///     Evaluates an advanced filter against an EventGridEvent with array filtering support.
    /// </summary>
    private static bool AcceptsAdvancedFilter(
        AdvancedFilterSetting filter,
        EventGridEvent gridEvent,
        bool enableArrayFiltering
    )
    {
        var keyExists = gridEvent.TryGetValue(filter.Key, out var value);
        var valueIsNull = keyExists && value == null;

        // Handle null check operators specially - they evaluate based on key existence
        switch (filter.OperatorType)
        {
            case AdvancedFilterSetting.AdvancedFilterOperatorType.IsNullOrUndefined:
                return !keyExists || valueIsNull;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull:
                return keyExists && !valueIsNull;
        }

        if (!keyExists)
        {
            return MatchesWhenKeyMissing(filter.OperatorType);
        }

        return EvaluateWithArraySupport(filter, value, enableArrayFiltering);
    }

    extension(FilterSetting filter)
    {
        /// <summary>
        ///     Checks if the filter accepts a SimulatorEvent (schema-agnostic).
        /// </summary>
        public bool AcceptsEvent(SimulatorEvent simulatorEvent)
        {
            if (filter == null)
            {
                return true;
            }

            var subject = simulatorEvent.Subject;

            // Check event type filter
            var retVal =
                filter.IncludedEventTypes == null
                || filter.IncludedEventTypes.Contains("All")
                || filter.IncludedEventTypes.Contains(simulatorEvent.EventType);

            // Check subject begins with filter
            retVal =
                retVal
                && (
                    string.IsNullOrWhiteSpace(filter.SubjectBeginsWith)
                    || subject.StartsWith(
                        filter.SubjectBeginsWith,
                        filter.IsSubjectCaseSensitive
                            ? StringComparison.Ordinal
                            : StringComparison.OrdinalIgnoreCase
                    )
                );

            // Check subject ends with filter
            retVal =
                retVal
                && (
                    string.IsNullOrWhiteSpace(filter.SubjectEndsWith)
                    || subject.EndsWith(
                        filter.SubjectEndsWith,
                        filter.IsSubjectCaseSensitive
                            ? StringComparison.Ordinal
                            : StringComparison.OrdinalIgnoreCase
                    )
                );

            // Check advanced filters
            retVal =
                retVal
                && (filter.AdvancedFilters ?? Array.Empty<AdvancedFilterSetting>()).All(af =>
                    AcceptsAdvancedFilter(
                        af,
                        simulatorEvent,
                        filter.EnableAdvancedFilteringOnArrays
                    )
                );

            return retVal;
        }

        /// <summary>
        ///     Checks if the filter accepts an EventGridEvent (legacy support).
        /// </summary>
        public bool AcceptsEvent(EventGridEvent gridEvent)
        {
            if (filter == null)
            {
                return true;
            }

            // we have a filter to parse
            var retVal =
                filter.IncludedEventTypes == null
                || filter.IncludedEventTypes.Contains("All")
                || filter.IncludedEventTypes.Contains(gridEvent.EventType);

            var subject = gridEvent.Subject;

            // short circuit if we have decided the event type is not acceptable
            retVal =
                retVal
                && (
                    string.IsNullOrWhiteSpace(filter.SubjectBeginsWith)
                    || subject.StartsWith(
                        filter.SubjectBeginsWith,
                        filter.IsSubjectCaseSensitive
                            ? StringComparison.Ordinal
                            : StringComparison.OrdinalIgnoreCase
                    )
                );

            // again, don't bother doing the comparison if we have already decided not to allow the event through the filter
            retVal =
                retVal
                && (
                    string.IsNullOrWhiteSpace(filter.SubjectEndsWith)
                    || subject.EndsWith(
                        filter.SubjectEndsWith,
                        filter.IsSubjectCaseSensitive
                            ? StringComparison.Ordinal
                            : StringComparison.OrdinalIgnoreCase
                    )
                );

            retVal =
                retVal
                && (filter.AdvancedFilters ?? Array.Empty<AdvancedFilterSetting>()).All(af =>
                    AcceptsAdvancedFilter(af, gridEvent, filter.EnableAdvancedFilteringOnArrays)
                );

            return retVal;
        }
    }
}
