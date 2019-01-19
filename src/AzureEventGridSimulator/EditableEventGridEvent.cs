using Microsoft.Azure.EventGrid.Models;
using Newtonsoft.Json;

namespace AzureEventGridSimulator
{
    public class EditableEventGridEvent : EventGridEvent
    {
        [JsonProperty("metadataVersion")]
        public new string MetadataVersion { get; set; }
    }
}
