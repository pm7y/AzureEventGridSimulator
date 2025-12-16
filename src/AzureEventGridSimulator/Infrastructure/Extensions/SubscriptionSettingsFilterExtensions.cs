using System;
using System.Linq;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Newtonsoft.Json.Linq;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class SubscriptionSettingsFilterExtensions
{
    /// <summary>
    /// Checks if the filter accepts a SimulatorEvent (schema-agnostic).
    /// </summary>
    public static bool AcceptsEvent(this FilterSetting filter, SimulatorEvent simulatorEvent)
    {
        if (filter == null)
        {
            return true;
        }

        // Extract common properties from SimulatorEvent
        var eventType = simulatorEvent.EventType;
        var subject = simulatorEvent.Subject ?? "";
        var data = simulatorEvent.Data;

        // Check event type filter
        var retVal = filter.IncludedEventTypes == null
                     || filter.IncludedEventTypes.Contains("All")
                     || filter.IncludedEventTypes.Contains(eventType);

        // Check subject begins with filter
        retVal = retVal
                 && (string.IsNullOrWhiteSpace(filter.SubjectBeginsWith)
                     || subject.StartsWith(filter.SubjectBeginsWith, filter.IsSubjectCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

        // Check subject ends with filter
        retVal = retVal
                 && (string.IsNullOrWhiteSpace(filter.SubjectEndsWith)
                     || subject.EndsWith(filter.SubjectEndsWith, filter.IsSubjectCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

        // Check advanced filters
        retVal = retVal && (filter.AdvancedFilters ?? Array.Empty<AdvancedFilterSetting>()).All(af => af.AcceptsEvent(simulatorEvent));

        return retVal;
    }

    /// <summary>
    /// Checks if the filter accepts an EventGridEvent (legacy support).
    /// </summary>
    public static bool AcceptsEvent(this FilterSetting filter, EventGridEvent gridEvent)
    {
        var retVal = filter == null;

        if (retVal)
        {
            return true;
        }

        // we have a filter to parse
        retVal = filter.IncludedEventTypes == null
                 || filter.IncludedEventTypes.Contains("All")
                 || filter.IncludedEventTypes.Contains(gridEvent.EventType);

        // short circuit if we have decided the event type is not acceptable
        retVal = retVal
                 && (string.IsNullOrWhiteSpace(filter.SubjectBeginsWith)
                     || gridEvent.Subject.StartsWith(filter.SubjectBeginsWith, filter.IsSubjectCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

        // again, don't bother doing the comparison if we have already decided not to allow the event through the filter
        retVal = retVal
                 && (string.IsNullOrWhiteSpace(filter.SubjectEndsWith)
                     || gridEvent.Subject.EndsWith(filter.SubjectEndsWith, filter.IsSubjectCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

        retVal = retVal && (filter.AdvancedFilters ?? Array.Empty<AdvancedFilterSetting>()).All(af => af.AcceptsEvent(gridEvent));

        return retVal;
    }

    private static bool AcceptsEvent(this AdvancedFilterSetting filter, SimulatorEvent simulatorEvent)
    {
        if (filter == null)
        {
            return true;
        }

        // filter is not null
        if (!simulatorEvent.TryGetValue(filter.Key, out var value))
        {
            return false;
        }

        return EvaluateAdvancedFilter(filter, value);
    }

    private static bool AcceptsEvent(this AdvancedFilterSetting filter, EventGridEvent gridEvent)
    {
        var retVal = filter == null;

        if (retVal)
        {
            return true;
        }

        // filter is not null
        if (!gridEvent.TryGetValue(filter.Key, out var value))
        {
            return false;
        }

        return EvaluateAdvancedFilter(filter, value);
    }

    private static bool EvaluateAdvancedFilter(AdvancedFilterSetting filter, object value)
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
                retVal = Try(() => (filter.Values ?? Array.Empty<object>()).Select(v => v.ToNumber()).Contains(value.ToNumber()));
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn:
                retVal = Try(() => !(filter.Values ?? Array.Empty<object>()).Select(v => v.ToNumber()).Contains(value.ToNumber()));
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.BoolEquals:
                retVal = Try(() => Convert.ToBoolean(value) == Convert.ToBoolean(filter.Value));
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains:
                {
                    var valueAsString = value as string;
                    retVal = Try(() => !string.IsNullOrEmpty(valueAsString) &&
                                       (filter.Values ?? Array.Empty<object>())
                                           .Select(v => Convert.ToString(v))
                                           .Where(v => !string.IsNullOrEmpty(v))
                                           .Any(filterValue => valueAsString.Contains(filterValue, StringComparison.OrdinalIgnoreCase)));
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith:
                {
                    // null or empty values cannot be considered to be the start character of a string
                    var valueAsString = value as string;
                    retVal = Try(() => !string.IsNullOrEmpty(valueAsString) &&
                                       (filter.Values ?? Array.Empty<object>())
                                           .Select(v => Convert.ToString(v))
                                           .Where(v => !string.IsNullOrEmpty(v))
                                           .Any(filterValue => valueAsString.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase)));
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith:
                {
                    // null or empty values cannot be considered to be the end character of a string
                    var valueAsString = value as string;
                    var filterValueAsString = filter.Value as string;

                    retVal = Try(() => !string.IsNullOrEmpty(filterValueAsString) &&
                                       !string.IsNullOrEmpty(valueAsString) &&
                                       valueAsString.EndsWith(filterValueAsString, StringComparison.OrdinalIgnoreCase));
                }
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn:
                retVal = Try(() =>
                                 (filter.Values ?? Array.Empty<object>()).Select(v => Convert.ToString(v)?.ToUpper()).Contains(Convert.ToString(value)?.ToUpper())
                                 );
                break;
            case AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn:
                retVal = Try(() => !(filter.Values ?? Array.Empty<object>()).Select(v => Convert.ToString(v)?.ToUpper()).Contains(Convert.ToString(value)?.ToUpper()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(AdvancedFilterSetting.AdvancedFilterOperatorType), "Unknown filter operator");
        }

        return retVal;
    }

    private static bool TryGetValue(this SimulatorEvent simulatorEvent, string key, out object value)
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
                if ((split[0] == "Data" || split[0] == "data") && simulatorEvent.Data != null && split.Length > 1)
                {
                    var tmpValue = simulatorEvent.Data;
                    for (var i = 1; i < split.Length; i++)
                    {
                        if (tmpValue == null || !JObject.FromObject(tmpValue).TryGetValue(split[i], out var dataValue))
                        {
                            return false;
                        }
                        tmpValue = dataValue.ToObject<object>();
                    }
                    if (tmpValue != null)
                    {
                        value = tmpValue;
                        return true;
                    }
                }
                return false;
        }
    }

    private static double ToNumber(this object value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "null is not convertible to a number in this implementation");
        }

        return Convert.ToDouble(value);
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

    private static bool TryGetValue(this EventGridEvent gridEvent, string key, out object value)
    {
        var retval = false;
        value = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            return retval;
        }

        switch (key)
        {
            case nameof(gridEvent.Id):
                value = gridEvent.Id;
                retval = true;
                break;
            case nameof(gridEvent.Topic):
                value = gridEvent.Topic;
                retval = true;
                break;
            case nameof(gridEvent.Subject):
                value = gridEvent.Subject;
                retval = true;
                break;
            case nameof(gridEvent.EventType):
                value = gridEvent.EventType;
                retval = true;
                break;
            case nameof(gridEvent.DataVersion):
                value = gridEvent.DataVersion;
                retval = true;
                break;
            case nameof(gridEvent.Data):
                value = gridEvent.Data;
                retval = true;
                break;
            default:
                var split = key.Split('.');
                if (split[0] != nameof(gridEvent.Data) || gridEvent.Data == null || split.Length <= 1)
                {
                    break;
                }
                var tmpValue = gridEvent.Data;
                for (var i = 1; i < split.Length; i++)
                {
                    // look for the property on the grid event data object
                    if (tmpValue == null || !JObject.FromObject(tmpValue).TryGetValue(split[i], out var dataValue))
                    {
                        tmpValue = null;
                        break;
                    }
                    tmpValue = dataValue.ToObject<object>();
                }
                if (tmpValue != null)
                {
                    retval = true;
                    value = tmpValue;
                }

                break;

        }

        return retval;
    }
}
