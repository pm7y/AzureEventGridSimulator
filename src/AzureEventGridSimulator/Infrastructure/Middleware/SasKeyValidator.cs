using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using AzureEventGridSimulator.Domain;
using Microsoft.Net.Http.Headers;

namespace AzureEventGridSimulator.Infrastructure.Middleware;

/// <summary>
///     Result of SAS key validation.
/// </summary>
public record SasValidationResult(bool IsValid, SasValidationFailureReason? FailureReason = null);

/// <summary>
///     Reasons for SAS validation failure.
/// </summary>
public enum SasValidationFailureReason
{
    /// <summary>
    ///     No authorization header was provided.
    /// </summary>
    MissingKey,

    /// <summary>
    ///     The aeg-sas-key value did not match.
    /// </summary>
    KeyMismatch,

    /// <summary>
    ///     The key is not a valid Base-64 string.
    /// </summary>
    InvalidBase64,

    /// <summary>
    ///     The token has expired.
    /// </summary>
    TokenExpired,

    /// <summary>
    ///     The token signature did not match.
    /// </summary>
    SignatureMismatch,

    /// <summary>
    ///     The token format is invalid.
    /// </summary>
    InvalidTokenFormat,

    /// <summary>
    ///     The key value is empty. Azure returns 400 for this case.
    /// </summary>
    EmptyKey,

    /// <summary>
    ///     Bearer token provided but not supported (AAD auth not configured).
    /// </summary>
    BearerTokenInvalid,

    /// <summary>
    ///     Unsupported authorization scheme (not SharedAccessSignature or Bearer).
    /// </summary>
    UnsupportedAuthScheme,
}

public class SasKeyValidator(TimeProvider timeProvider, ILogger<SasKeyValidator> logger)
{
    public bool IsValid(IHeaderDictionary requestHeaders, string topicKey)
    {
        return Validate(requestHeaders, topicKey).IsValid;
    }

    public SasValidationResult Validate(IHeaderDictionary requestHeaders, string topicKey)
    {
        if (
            requestHeaders.Any(h =>
                string.Equals(Constants.AegSasKeyHeader, h.Key, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var keyValue = requestHeaders[Constants.AegSasKeyHeader].FirstOrDefault();

            // Azure returns 400 Bad Request for empty keys
            if (string.IsNullOrEmpty(keyValue))
            {
                logger.LogError("'aeg-sas-key' value is empty!");
                return new SasValidationResult(false, SasValidationFailureReason.EmptyKey);
            }

            if (!string.Equals(keyValue, topicKey))
            {
                logger.LogError("'aeg-sas-key' value did not match the expected value!");
                return new SasValidationResult(false, SasValidationFailureReason.KeyMismatch);
            }

            return new SasValidationResult(true);
        }

        if (
            requestHeaders.Any(h =>
                string.Equals(
                    Constants.AegSasTokenHeader,
                    h.Key,
                    StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            var token = requestHeaders[Constants.AegSasTokenHeader].FirstOrDefault();
            if (token == null)
                return new SasValidationResult(false, SasValidationFailureReason.MissingKey);

            var tokenResult = ValidateToken(token, topicKey);
            if (!tokenResult.IsValid)
            {
                logger.LogError("'aeg-sas-token' value did not match the expected value!");
                return tokenResult;
            }

            return new SasValidationResult(true);
        }

        if (
            requestHeaders.Any(h =>
                string.Equals(HeaderNames.Authorization, h.Key, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var token = requestHeaders[HeaderNames.Authorization].ToString();
            if (
                token.StartsWith(Constants.SasAuthorizationType, StringComparison.OrdinalIgnoreCase)
            )
            {
                var tokenValue = token
                    .Replace(Constants.SasAuthorizationType, "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                var tokenResult = ValidateToken(tokenValue, topicKey);
                if (!tokenResult.IsValid)
                {
                    logger.LogError(
                        "'Authorization: SharedAccessSignature' value did not match the expected value!"
                    );
                    return tokenResult;
                }

                return new SasValidationResult(true);
            }

            // Check for Bearer token (Azure supports AAD auth, but we don't)
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError("Bearer token authentication is not supported by the simulator.");
                return new SasValidationResult(
                    false,
                    SasValidationFailureReason.BearerTokenInvalid
                );
            }

            // Basic auth or other unsupported schemes
            logger.LogError(
                "Unsupported Authorization header type. Only SharedAccessSignature is supported."
            );
            return new SasValidationResult(false, SasValidationFailureReason.UnsupportedAuthScheme);
        }

        return new SasValidationResult(false, SasValidationFailureReason.MissingKey);
    }

    private SasValidationResult ValidateToken(string token, string key)
    {
        var query = HttpUtility.ParseQueryString(token);
        var resource = query["r"];
        var expiration = query["e"];
        var signature = query["s"];

        // All three parameters are required
        if (
            string.IsNullOrEmpty(resource)
            || string.IsNullOrEmpty(expiration)
            || string.IsNullOrEmpty(signature)
        )
            return new SasValidationResult(false, SasValidationFailureReason.InvalidTokenFormat);

        // Parse expiration as Unix epoch seconds
        if (
            !long.TryParse(expiration, out var expiryEpoch)
            || DateTimeOffset.FromUnixTimeSeconds(expiryEpoch) <= timeProvider.GetUtcNow()
        )
            return new SasValidationResult(false, SasValidationFailureReason.TokenExpired);

        // The string to sign is: {resource}\n{expiryEpoch}
        // This matches Azure Event Grid's SAS token format
        var decodedResource = HttpUtility.UrlDecode(resource);
        var stringToSign = $"{decodedResource}\n{expiration}";

        try
        {
            using var hmac = new HMACSHA256(Convert.FromBase64String(key));
            var computedSignature = Convert.ToBase64String(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign))
            );

            // ParseQueryString already decodes URL-encoded values, so signature is ready to compare
            // Note: Don't call UrlDecode again as it would convert '+' to space
            if (string.Equals(signature, computedSignature, StringComparison.Ordinal))
                return new SasValidationResult(true);

            // Sanitize signature to prevent log forging by escaping all control characters
            var sanitizedSignature = SanitizeForLogging(signature);
            logger.LogWarning(
                "SAS token signature mismatch. Expected: {Expected}, Got: {Actual}",
                computedSignature,
                sanitizedSignature
            );

            return new SasValidationResult(false, SasValidationFailureReason.SignatureMismatch);
        }
        catch (FormatException)
        {
            return new SasValidationResult(false, SasValidationFailureReason.InvalidBase64);
        }
    }

    /// <summary>
    ///     Sanitizes a string for logging by escaping all control characters (code points &lt; 32)
    ///     to prevent log forging attacks.
    /// </summary>
    /// <param name="input">The string to sanitize</param>
    /// <returns>A sanitized string with control characters escaped</returns>
    private static string SanitizeForLogging(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c < 32) // Control characters have code points less than 32 (space)
            {
                sb.Append(
                    c switch
                    {
                        '\n' => "\\n",
                        '\r' => "\\r",
                        '\t' => "\\t",
                        '\f' => "\\f",
                        '\b' => "\\b",
                        '\0' => "\\0",
                        '\a' => "\\a",
                        '\v' => "\\v",
                        _ => $"\\x{((int)c):X2}" // Escape other control chars as hex
                    }
                );
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
