namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Thrown when a request can't be parsed into events, carrying the error detail code for the
///     response so that it doesn't have to be worked out from the message text.
/// </summary>
/// <remarks>
///     Derives from <see cref="InvalidOperationException" />, which the parsers throw for the
///     failures that use the default InputJsonInvalid code.
/// </remarks>
/// <param name="message">The error message for the response.</param>
/// <param name="errorCode">The error detail code for the response (see ErrorDetailCodes).</param>
public sealed class EventParseException(string message, string errorCode)
    : InvalidOperationException(message)
{
    /// <summary>
    ///     Gets the error detail code for the response.
    /// </summary>
    public string ErrorCode { get; } = errorCode;
}
