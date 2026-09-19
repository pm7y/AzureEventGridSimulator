namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Rules from the CloudEvents HTTP protocol binding that routing, schema detection and
///     validation must agree on.
/// </summary>
internal static class CloudEventsHttp
{
    /// <summary>
    ///     Checks whether a request uses CloudEvents binary content mode.
    ///     Azure detects binary mode when any of the required-attribute ce-* headers is present,
    ///     then reports any that are missing when it parses the event.
    /// </summary>
    /// <param name="headers">The request headers.</param>
    /// <returns>True if the request is in binary mode.</returns>
    public static bool IsBinaryMode(IHeaderDictionary headers)
    {
        return headers.ContainsKey(Constants.CeSpecVersionHeader)
            || headers.ContainsKey(Constants.CeIdHeader)
            || headers.ContainsKey(Constants.CeSourceHeader)
            || headers.ContainsKey(Constants.CeTypeHeader);
    }
}
