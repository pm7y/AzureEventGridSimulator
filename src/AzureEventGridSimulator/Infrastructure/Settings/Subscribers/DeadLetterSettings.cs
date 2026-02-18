using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Dead-letter destination settings.
///     Events that cannot be delivered are written to JSON files in the specified folder.
/// </summary>
public class DeadLetterSettings
{
    /// <summary>
    ///     Gets or sets whether dead-lettering is enabled.
    ///     Default is true.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the base folder path for dead-letter files.
    ///     Files are written to {FolderPath}/{TopicName}/{SubscriberName}/{timestamp}_{eventId}.json
    ///     Default is "./dead-letters".
    /// </summary>
    [JsonPropertyName("folderPath")]
    public string FolderPath { get; set; } = "./dead-letters";

    public void Validate()
    {
        // Path validation is performed at runtime when writing files
        // Empty/null path will use the default
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            FolderPath = "./dead-letters";
        }
    }
}
