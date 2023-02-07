namespace AzureEventGridSimulator.Infrastructure.Extensions;

using System;
using System.Linq;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;

public static class SubscriptionSettingsFilterExtensions
{
    public static bool AcceptsEvent(this FilterSetting filter, IEvent gridEvent)
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

    private static bool AcceptsEvent(this AdvancedFilterSetting filter, IEvent gridEvent)
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

        switch (filter.OperatorType)
        {
            case AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThan:
                retVal = Try(() => value.ToNumber() > filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThanOrEquals:
                retVal = Try(() => value.ToNumber() >= filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.NumberLessThan:
                retVal = Try(() => value.ToNumber() < filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.NumberLessThanOrEquals:
                retVal = Try(() => value.ToNumber() <= filter.Value.ToNumber());
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.NumberIn:
                retVal = Try(() => (filter.Values ?? Array.Empty<object>()).Select(v => v.ToNumber()).Contains(value.ToNumber()));
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn:
                retVal = Try(() => !(filter.Values ?? Array.Empty<object>()).Select(v => v.ToNumber()).Contains(value.ToNumber()));
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.BoolEquals:
                retVal = Try(() => Convert.ToBoolean(value) == Convert.ToBoolean(filter.Value));
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.StringContains:
                {
                    // a string cannot be considered to contain null or and empty string
                    var valueAsString = value as string;
                    var filterValueAsString = filter.Value as string;

                    retVal = Try(() => !string.IsNullOrEmpty(filterValueAsString) &&
                                       !string.IsNullOrEmpty(valueAsString) &&
                                       valueAsString.Contains(filterValueAsString, StringComparison.OrdinalIgnoreCase));
                }
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith:
                {
                    // null or empty values cannot be considered to be the beginning character of a string
                    var valueAsString = value as string;
                    var filterValueAsString = filter.Value as string;

                    retVal = Try(() => !string.IsNullOrEmpty(filterValueAsString) &&
                                       !string.IsNullOrEmpty(valueAsString) &&
                                       valueAsString.StartsWith(filterValueAsString, StringComparison.OrdinalIgnoreCase));
                }
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith:
                {
                    // null or empty values cannot be considered to be the end character of a string
                    var valueAsString = value as string;
                    var filterValueAsString = filter.Value as string;

                    retVal = Try(() => !string.IsNullOrEmpty(filterValueAsString) &&
                                       !string.IsNullOrEmpty(valueAsString) &&
                                       valueAsString.EndsWith(filterValueAsString, StringComparison.OrdinalIgnoreCase));
                }
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.StringIn:
                retVal = Try(() => (filter.Values ?? Array.Empty<object>()).Select(v => Convert.ToString(v)?.ToUpper()).Contains(Convert.ToString(value)?.ToUpper()));
                break;
            case AdvancedFilterSetting.OperatorTypeEnum.StringNotIn:
                retVal = Try(() => !(filter.Values ?? Array.Empty<object>()).Select(v => Convert.ToString(v)?.ToUpper()).Contains(Convert.ToString(value)?.ToUpper()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(AdvancedFilterSetting.OperatorTypeEnum), "Unknown filter operator");
        }

        return retVal;
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
}
