using System;
using System.Linq;
using AzureEventGridSimulator.Settings;
using Newtonsoft.Json.Linq;

namespace AzureEventGridSimulator.Extensions
{
    public static class SubscriptionSettingsFilterExtensions
    {
        public static bool AcceptsEvent(this FilterSetting filter, EventGridEvent gridEvent)
        {
            var retVal = filter == null;

            if (!retVal)
            {
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

                retVal = retVal && (filter.AdvancedFilters ?? new AdvancedFilterSetting[0]).All(af => af.AcceptsEvent(gridEvent));
            }

            return retVal;
        }

        public static bool AcceptsEvent(this AdvancedFilterSetting filter, EventGridEvent gridEvent)
        {
            var retVal = filter == null;

            if (!retVal)
            {
                // filter is not null
                if (gridEvent.TryGetValue(filter.Key, out object value))
                {
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
                            retVal = Try(() => (filter.Values ?? new object[0]).Select(v => v.ToNumber()).Contains(value.ToNumber()));
                            break;
                        case AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn:
                            retVal = Try(() => !(filter.Values ?? new object[0]).Select(v => v.ToNumber()).Contains(value.ToNumber()));
                            break;
                        case AdvancedFilterSetting.OperatorTypeEnum.BoolEquals:
                            retVal = Try(() => Convert.ToBoolean(value) == Convert.ToBoolean(filter.Value));
                            break;
                        case AdvancedFilterSetting.OperatorTypeEnum.StringContains:
                            // a string cannot be considered to contain null or and empty string
                            retVal = Try(() => !string.IsNullOrEmpty(Convert.ToString(filter.Value)) && Convert.ToString(value).ToUpper().Contains(Convert.ToString(filter.Value).ToUpper()));
                            break;
                        case AdvancedFilterSetting.OperatorTypeEnum.StringBeginsWith:
                            // null or empty values cannot be considered to be the beginning character of a string 
                            retVal = Try(() => !string.IsNullOrEmpty(Convert.ToString(filter.Value)) && Convert.ToString(value).ToUpper().StartsWith(Convert.ToString(filter.Value).ToUpper()));
                            break;
                        case AdvancedFilterSetting.OperatorTypeEnum.StringEndsWith:
                            // null or empty values cannot be considered to be the end character of a string 
                            retVal = Try(() => !string.IsNullOrEmpty(Convert.ToString(filter.Value)) && Convert.ToString(value).ToUpper().EndsWith(Convert.ToString(filter.Value).ToUpper()));
                            break;
                        case AdvancedFilterSetting.OperatorTypeEnum.StringIn:
                            retVal = Try(() => (filter.Values ?? new object[0]).Select(v => Convert.ToString(v).ToUpper()).Contains(Convert.ToString(value).ToUpper()));
                            break;
                        case AdvancedFilterSetting.OperatorTypeEnum.StringNotIn:
                            retVal = Try(() => !(filter.Values ?? new object[0]).Select(v => Convert.ToString(v).ToUpper()).Contains(Convert.ToString(value).ToUpper()));
                            break;
                    }
                }
            }

            return retVal;
        }

        private static double ToNumber(this object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), "null is not convertable to a number in this implementation");
            }

            return Convert.ToDouble(value);
        }

        private static bool Try(Func<bool> function, bool valueOnException = false)
        {
            try
            {
                return function();
            }
            catch (Exception x)
            {
                System.Diagnostics.Debug.WriteLine(x.Message);
                return valueOnException;
            }
        }

        public static bool TryGetValue(this EventGridEvent gridEvent, string key, out object value)
        {
            var retval = false;
            value = null;

            if (!string.IsNullOrWhiteSpace(key))
            {
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
                        if (split[0] == (nameof(gridEvent.Data)) && gridEvent.Data != null && split.Length > 1)
                        {
                            // look for the property on the grid event data object
                            if (JObject.FromObject(gridEvent.Data).TryGetValue(split[1], out var dataValue))
                            {
                                value = dataValue.ToObject<object>();
                                retval = true;
                            }
                        }
                        break;
                }
            }

            return retval;
        }
    }
}
