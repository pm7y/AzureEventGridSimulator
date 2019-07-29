using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class FilterSetting
    {
        [JsonProperty(PropertyName = "includedEventTypes", Required = Required.AllowNull)]
        public ICollection<string> IncludedEventTypes { get; set; }

        [JsonProperty(PropertyName = "isSubjectCaseSensitive", Required = Required.AllowNull)]
        public bool IsSubjectCaseSensitive { get; set; }

        [JsonProperty(PropertyName = "subjectBeginsWith", Required = Required.AllowNull)]
        public string SubjectBeginsWith { get; set; }

        [JsonProperty(PropertyName = "subjectEndsWith", Required = Required.AllowNull)]
        public string SubjectEndsWith { get; set; }

        [JsonProperty(PropertyName = "advancedFilters", Required = Required.AllowNull)]
        public ICollection<AdvancedFilterSetting> AdvancedFilters { get; set; }

        internal void Validate()
        {
            if (AdvancedFilters?.Count > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(AdvancedFilters), "Advanced filtering is limited to five advanced filters per event grid subscription.");
            }

            foreach (var advancedFilter in AdvancedFilters?? new AdvancedFilterSetting[0])
            {
                advancedFilter.Validate();
            }
        }
    }
}
