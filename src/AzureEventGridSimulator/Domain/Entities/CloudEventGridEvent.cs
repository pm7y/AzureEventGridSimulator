using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Entities;
public class CloudEventGridEvent
{
    [DataMember(Name = "id")]
    [JsonProperty("id")]
    public string Id { get; set; }
    [DataMember(Name = "source")]
    [JsonProperty("source")]
    public string Source { get; set; }
    [DataMember(Name = "type")]
    [JsonProperty("type")]
    public string Type { get; set; }
    [DataMember(Name = "data_base64")]
    [JsonProperty("data_base64")]
    public string Data_Base64 { get; set; }
    [DataMember(Name = "time")]
    [JsonProperty("time")]
    public DateTimeOffset Time { get; set; }
    [DataMember(Name = "specversion")]
    [JsonProperty("specversion")]
    public string SpecVersion { get; set; }
    [DataMember(Name = "dataschema")]
    [JsonProperty("dataschema")]
    public string DataSchema { get; set; }
    [DataMember(Name = "datacontenttype")]
    [JsonProperty("datacontenttype")]
    public string DataContentType { get; set; }
    [DataMember(Name = "subject")]
    [JsonProperty("subject")]
    public string Subject { get; set; }
}
