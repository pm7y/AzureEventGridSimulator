using System;
using System.Linq;
using AzureEventGridSimulator.Settings;

namespace AzureEventGridSimulator.Extensions
{
    internal static class SubscriptionSettingsFilterExtensions
    {
        public static bool AcceptsEvent(this FilterSetting filter, EventGridEvent gridEvent)
        {
            bool retVal = filter == null;

            if (!retVal)
            {
                // we have a filter to parse
                if (filter.IncludedEventTypes == null
                    || filter.IncludedEventTypes.Contains("All")
                    || filter.IncludedEventTypes.Contains(gridEvent.EventType))
                {
                    retVal = true;
                }

                // short circuit if we have decided the event type is not acceptable

                if (retVal && (string.IsNullOrWhiteSpace(filter.SubjectBeginsWith)
                    || gridEvent.Subject.StartsWith(filter.SubjectBeginsWith, filter.IsSubjectCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)))
                {
                    retVal = true;
                }

                // again, don't bother doing the comparison if we have already decided not to allow the event through the filter
                if (retVal && (string.IsNullOrWhiteSpace(filter.SubjectEndsWith)
                    || gridEvent.Subject.EndsWith(filter.SubjectEndsWith, filter.IsSubjectCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)))
                {
                    retVal = true;
                }
            }

            return retVal;
        }
    }
}
