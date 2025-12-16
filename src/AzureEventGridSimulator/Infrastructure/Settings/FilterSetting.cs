using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings;

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
        // Azure Event Grid allows up to 25 advanced filters per subscription
        if (AdvancedFilters?.Count > 25)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AdvancedFilters),
                "Advanced filtering is limited to 25 advanced filters per event grid subscription."
            );
        }

        foreach (var advancedFilter in AdvancedFilters ?? Array.Empty<AdvancedFilterSetting>())
        {
            advancedFilter.Validate();
        }
    }
}
