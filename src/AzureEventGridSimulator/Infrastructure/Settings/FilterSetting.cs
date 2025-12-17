using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class FilterSetting
{
    [JsonPropertyName("includedEventTypes")]
    public ICollection<string> IncludedEventTypes { get; set; }

    [JsonPropertyName("isSubjectCaseSensitive")]
    public bool IsSubjectCaseSensitive { get; set; }

    [JsonPropertyName("subjectBeginsWith")]
    public string SubjectBeginsWith { get; set; }

    [JsonPropertyName("subjectEndsWith")]
    public string SubjectEndsWith { get; set; }

    [JsonPropertyName("advancedFilters")]
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
