using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using AzureEventGridSimulator.Domain;
using Microsoft.Net.Http.Headers;

namespace AzureEventGridSimulator.Infrastructure.Middleware;

/// <summary>
/// Result of SAS key validation.
/// </summary>
public record SasValidationResult(bool IsValid, SasValidationFailureReason? FailureReason = null);

/// <summary>
/// Reasons for SAS validation failure.
/// </summary>
public enum SasValidationFailureReason
{
    /// <summary>
    /// No authorization header was provided.
    /// </summary>
    MissingKey,

    /// <summary>
    /// The aeg-sas-key value did not match.
    /// </summary>
    KeyMismatch,

    /// <summary>
    /// The key is not a valid Base-64 string.
    /// </summary>
    InvalidBase64,

    /// <summary>
    /// The token has expired.
    /// </summary>
    TokenExpired,

    /// <summary>
    /// The token signature did not match.
    /// </summary>
    SignatureMismatch,

    /// <summary>
    /// The token format is invalid.
    /// </summary>
    InvalidTokenFormat,
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
            if (!string.Equals(requestHeaders[Constants.AegSasKeyHeader], topicKey))
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

            return new SasValidationResult(true);
        }

        return new SasValidationResult(false, SasValidationFailureReason.MissingKey);
    }

    private SasValidationResult ValidateToken(string token, string key)
    {
        var query = HttpUtility.ParseQueryString(token);
        var decodedResource = HttpUtility.UrlDecode(query["r"], Encoding.UTF8);
        var decodedExpiration = HttpUtility.UrlDecode(query["e"], Encoding.UTF8);
        var encodedSignature = query["s"];

        if (string.IsNullOrEmpty(decodedExpiration) || string.IsNullOrEmpty(encodedSignature))
        {
            return new SasValidationResult(false, SasValidationFailureReason.InvalidTokenFormat);
        }

        if (
            !DateTimeOffset.TryParse(
                decodedExpiration,
                CultureInfo.InvariantCulture,
                out var tokenExpiryDateTime
            )
            || tokenExpiryDateTime.ToUniversalTime() <= timeProvider.GetUtcNow()
        )
        {
            return new SasValidationResult(false, SasValidationFailureReason.TokenExpired);
        }

        var encodedResource = HttpUtility.UrlEncode(decodedResource);
        var encodedExpiration = HttpUtility.UrlEncode(decodedExpiration);

        var unsignedSas = $"r={encodedResource}&e={encodedExpiration}";

        try
        {
            using var hmac = new HMACSHA256(Convert.FromBase64String(key));
            var signature = Convert.ToBase64String(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedSas))
            );
            var encodedComputedSignature = HttpUtility.UrlEncode(signature);

            if (encodedSignature == signature)
            {
                return new SasValidationResult(true);
            }

            logger.LogWarning(
                "{ExpectedSignature} != {MessageSignature}",
                encodedComputedSignature,
                signature
            );

            return new SasValidationResult(false, SasValidationFailureReason.SignatureMismatch);
        }
        catch (FormatException)
        {
            return new SasValidationResult(false, SasValidationFailureReason.InvalidBase64);
        }
    }
}
