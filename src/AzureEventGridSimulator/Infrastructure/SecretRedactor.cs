namespace AzureEventGridSimulator.Infrastructure;

/// <summary>
///     Keeps secrets (topic keys, broker credentials, certificate passwords) out of log output.
/// </summary>
public static class SecretRedactor
{
    /// <summary>
    ///     What a secret value in a connection string is replaced with.
    /// </summary>
    public const string RedactedValue = "***REDACTED***";

    // A configuration key names a secret when its last segment ends with one of these
    private static readonly string[] SecretKeySuffixes =
    [
        "key",
        "password",
        "connectionString",
        "sharedAccessKey",
    ];

    // Connection string settings whose values are credentials
    private static readonly string[] SecretConnectionStringSettings =
    [
        "SharedAccessKey",
        "SharedAccessSignature",
        "AccountKey",
    ];

    /// <summary>
    ///     Whether a configuration key (e.g. <c>topics:0:key</c>) holds a secret: its last
    ///     segment ends with key, password, connectionString or sharedAccessKey, ignoring case.
    /// </summary>
    public static bool IsSecretKey(string key)
    {
        var lastSegment = key[(key.LastIndexOf(':') + 1)..];

        return SecretKeySuffixes.Any(suffix =>
            lastSegment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    ///     Masks the SharedAccessKey, SharedAccessSignature and AccountKey values in a
    ///     connection string, wherever they appear, and leaves every other setting (such as
    ///     the endpoint) readable.
    /// </summary>
    public static string? RedactConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return connectionString;
        }

        var settings = connectionString.Split(';');

        for (var i = 0; i < settings.Length; i++)
        {
            // Split on the first '=' only, because values such as SAS tokens contain '=' too
            var separator = settings[i].IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var name = settings[i][..separator].Trim();
            if (SecretConnectionStringSettings.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                settings[i] = settings[i][..(separator + 1)] + RedactedValue;
            }
        }

        return string.Join(';', settings);
    }
}
