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
    // The range of Unix times DateTimeOffset.FromUnixTimeSeconds accepts
    private static readonly long MinExpiryEpoch = DateTimeOffset.MinValue.ToUnixTimeSeconds();
    private static readonly long MaxExpiryEpoch = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    public SasValidationResult Validate(IHeaderDictionary requestHeaders, string topicKey)
    {
        // Header lookups ignore case
        if (requestHeaders.TryGetValue(Constants.AegSasKeyHeader, out var keyValues))
        {
            var keyValue = keyValues.FirstOrDefault();

            // Azure returns 400 Bad Request for empty keys
            if (string.IsNullOrEmpty(keyValue))
            {
                logger.LogError("'aeg-sas-key' value is empty!");
                return new SasValidationResult(false, SasValidationFailureReason.EmptyKey);
            }

            if (!SecretEquals(keyValue, topicKey))
            {
                logger.LogError("'aeg-sas-key' value did not match the expected value!");
                return new SasValidationResult(false, SasValidationFailureReason.KeyMismatch);
            }

            return new SasValidationResult(true);
        }

        if (requestHeaders.TryGetValue(Constants.AegSasTokenHeader, out var tokenValues))
        {
            var token = tokenValues.FirstOrDefault();
            if (token == null)
            {
                return new SasValidationResult(false, SasValidationFailureReason.MissingKey);
            }

            var tokenResult = ValidateToken(token, topicKey);
            if (!tokenResult.IsValid)
            {
                logger.LogError("'aeg-sas-token' value did not match the expected value!");
                return tokenResult;
            }

            return new SasValidationResult(true);
        }

        if (requestHeaders.TryGetValue(HeaderNames.Authorization, out var authValues))
        {
            // ToString() joins multiple values with commas
            var token = authValues.ToString();
            if (
                token.StartsWith(Constants.SasAuthorizationType, StringComparison.OrdinalIgnoreCase)
            )
            {
                // Remove only the leading scheme, not the same text further into the token
                var tokenValue = token[Constants.SasAuthorizationType.Length..].Trim();

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
        {
            return new SasValidationResult(false, SasValidationFailureReason.InvalidTokenFormat);
        }

        // Parse expiration as Unix epoch seconds. A value that isn't a number is reported as
        // expired, which is the 401 message users have always had for it.
        if (!long.TryParse(expiration, out var expiryEpoch))
        {
            return new SasValidationResult(false, SasValidationFailureReason.TokenExpired);
        }

        // A number too large or small to be a date (such as an expiry in milliseconds) would
        // make FromUnixTimeSeconds throw, so reject it as a malformed token
        if (expiryEpoch < MinExpiryEpoch || expiryEpoch > MaxExpiryEpoch)
        {
            return new SasValidationResult(false, SasValidationFailureReason.InvalidTokenFormat);
        }

        if (DateTimeOffset.FromUnixTimeSeconds(expiryEpoch) <= timeProvider.GetUtcNow())
        {
            return new SasValidationResult(false, SasValidationFailureReason.TokenExpired);
        }

        // The string to sign is: {resource}\n{expiryEpoch}
        // This matches Azure Event Grid's SAS token format
        var decodedResource = HttpUtility.UrlDecode(resource);
        var stringToSign = $"{decodedResource}\n{expiration}";

        try
        {
            var computedSignature = Convert.ToBase64String(
                HMACSHA256.HashData(
                    Convert.FromBase64String(key),
                    Encoding.UTF8.GetBytes(stringToSign)
                )
            );

            // ParseQueryString already decodes URL-encoded values, so signature is ready to compare
            // Note: Don't call UrlDecode again as it would convert '+' to space
            if (SecretEquals(signature, computedSignature))
            {
                return new SasValidationResult(true);
            }

            // Never log the computed signature: it's the HMAC of the caller's own resource and
            // expiry, so it would be a valid signature for them. Everything logged here comes
            // from the caller, so control characters are escaped to prevent log forging.
            logger.LogWarning(
                "SAS token signature mismatch. Got: {Actual}",
                SanitizeForLogging(signature)
            );
            logger.LogDebug(
                "The SAS token signature was checked against the string-to-sign '{Resource}\\n{Expiry}' (the decoded resource and the expiry, separated by a newline)",
                SanitizeForLogging(decodedResource),
                SanitizeForLogging(expiration)
            );

            return new SasValidationResult(false, SasValidationFailureReason.SignatureMismatch);
        }
        catch (FormatException)
        {
            return new SasValidationResult(false, SasValidationFailureReason.InvalidBase64);
        }
    }

    /// <summary>
    ///     Compares two secrets in constant time for a given length, so the time taken doesn't
    ///     reveal how much of the value matched.
    /// </summary>
    private static bool SecretEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b)
        );
    }

    /// <summary>
    ///     Sanitizes a string for logging by escaping all control characters (code points &lt; 32 and DEL at 127)
    ///     to prevent log forging attacks.
    /// </summary>
    /// <param name="input">The string to sanitize</param>
    /// <returns>A sanitized string with control characters escaped</returns>
    private static string SanitizeForLogging(string input)
    {
        // Use input.Length * 2 capacity to reduce reallocations when control characters expand
        // (e.g., '\n' becomes "\\n" which is 2 characters instead of 1)
        var sb = new StringBuilder(input.Length * 2);
        foreach (var c in input)
        {
            if (c < 32 || c == 127) // Control characters: code points < 32 and DEL (127)
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
                        (char)127 => "\\x7F", // DEL character
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
