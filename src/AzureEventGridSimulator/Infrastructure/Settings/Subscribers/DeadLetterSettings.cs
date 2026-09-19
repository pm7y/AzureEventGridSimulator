using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Dead-letter destination settings.
///     Events that cannot be delivered are written to JSON files in the specified folder.
/// </summary>
public class DeadLetterSettings
{
    private const string DefaultFolderPath = "./dead-letters";

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
    public string FolderPath { get; set; } = DefaultFolderPath;

    public void Validate()
    {
        // Path validation is performed at runtime when writing files
        ApplyDefaults();
    }

    /// <summary>
    ///     Replaces an empty/null folder path with the default. Safe to call more than once.
    /// </summary>
    internal void ApplyDefaults()
    {
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            FolderPath = DefaultFolderPath;
        }
    }
}
