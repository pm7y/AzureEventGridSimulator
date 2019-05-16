using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class FilterSetting
    {
        [JsonProperty(PropertyName = "includedEventTypes", Required = Required.AllowNull)]
        public string[] IncludedEventTypes { get; set; }

        [JsonProperty(PropertyName = "isSubjectCaseSensitive", Required = Required.AllowNull)]
        public bool IsSubjectCaseSensitive { get; set; } = false;

        [JsonProperty(PropertyName = "subjectBeginsWith", Required = Required.AllowNull)]
        public string SubjectBeginsWith { get; set; }

        [JsonProperty(PropertyName = "subjectEndsWith", Required = Required.AllowNull)]
        public string SubjectEndsWith { get; set; }
    }
}
