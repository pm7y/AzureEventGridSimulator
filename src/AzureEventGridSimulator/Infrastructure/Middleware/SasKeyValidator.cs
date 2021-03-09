using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Infrastructure.Middleware
{
    public class SasKeyValidator
    {
        private readonly ILogger<SasKeyValidator> _logger;

        public SasKeyValidator(ILogger<SasKeyValidator> logger)
        {
            _logger = logger;
        }

        public bool IsValid(IHeaderDictionary requestHeaders, string topicKey)
        {
            if (requestHeaders
                .Any(h => string.Equals("aeg-sas-key", h.Key, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.Equals(requestHeaders["aeg-sas-key"], topicKey))
                {
                    _logger.LogError("'aeg-sas-key' value did not match the expected value!");
                    return false;
                }

                _logger.LogTrace("'aeg-sas-key' header is valid");
                return true;
            }

            if (requestHeaders
                .Any(h => string.Equals("aeg-sas-token", h.Key, StringComparison.OrdinalIgnoreCase)))
            {
                var token = requestHeaders["aeg-sas-token"].First();
                if (!TokenIsValid(token, topicKey))
                {
                    _logger.LogError("'aeg-sas-token' value did not match the expected value!");
                    return false;
                }

                _logger.LogTrace("'aeg-sas-token' header is valid");
                return true;
            }

            if (requestHeaders
                .Any(h => string.Equals("Authorization", h.Key, StringComparison.OrdinalIgnoreCase)))
            {
                var token = requestHeaders["Authorization"].ToString();
                if (token.StartsWith("SharedAccessSignature") && !TokenIsValid(token.Replace("SharedAccessSignature", "").Trim(), topicKey))
                {
                    _logger.LogError("'Authorization: SharedAccessSignature' value did not match the expected value!");
                    return false;
                }

                _logger.LogTrace("'Authorization: SharedAccessSignature' header is valid");
                return true;
            }

            return false;
        }

        private bool TokenIsValid(string token, string key)
        {
            var query = HttpUtility.ParseQueryString(token);
            var decodedResource = HttpUtility.UrlDecode(query["r"], Encoding.UTF8);
            var decodedExpiration = HttpUtility.UrlDecode(query["e"], Encoding.UTF8);
            var encodedSignature = query["s"];

            if (!DateTime.TryParse(decodedExpiration, out var tokenExpiryDateTime) ||
                tokenExpiryDateTime.ToUniversalTime() <= DateTime.UtcNow)
            {
                return false;
            }

            var encodedResource = HttpUtility.UrlEncode(decodedResource);
            var encodedExpiration = HttpUtility.UrlEncode(decodedExpiration);

            var unsignedSas = $"r={encodedResource}&e={encodedExpiration}";

            using var hmac = new HMACSHA256(Convert.FromBase64String(key));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedSas)));
            var encodedComputedSignature = HttpUtility.UrlEncode(signature);

            if (encodedSignature == signature)
            {
                return true;
            }

            _logger.LogWarning("{ExpectedSignature} != {MessageSignature}", encodedComputedSignature, signature);

            return false;
        }
    }
}
