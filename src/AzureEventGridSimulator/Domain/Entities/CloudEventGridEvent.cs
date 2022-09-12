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

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException($"Required property '{nameof(Id)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(Source))
        {
            throw new InvalidOperationException($"Required property '{nameof(Source)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(Type))
        {
            throw new InvalidOperationException($"Required property '{nameof(Type)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(Data_Base64))
        {
            throw new InvalidOperationException($"Required property '{nameof(Data_Base64)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(DataSchema))
        {
            throw new InvalidOperationException($"Required property '{nameof(DataSchema)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(DataContentType))
        {
            throw new InvalidOperationException($"Required property '{nameof(DataContentType)}' was not set.");
        }
        
        if (string.IsNullOrWhiteSpace(Subject))
        {
            throw new InvalidOperationException($"Required property '{nameof(Subject)}' was not set.");
        }


    }

}
