using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AzureEventGridSimulator.Middleware
{
    public class SasKeyValidator : IAegSasHeaderValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public SasKeyValidator(IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public bool IsValid(IHeaderDictionary requestHeaders, string topicKey)
        {
            if (requestHeaders
                .Any(h => string.Equals("aeg-sas-key", h.Key, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.Equals(requestHeaders["aeg-sas-key"], topicKey))
                {
                    _logger.Error("'aeg-sas-key' value did not match configured value!");
                    return false;
                }

                _logger.Debug($"'aeg-sas-key' header is valid");
                return true;
            }

            if (requestHeaders
                .Any(h => string.Equals("aeg-sas-token", h.Key, StringComparison.OrdinalIgnoreCase)))
            {
                var token = requestHeaders["aeg-sas-token"];
                if (!TokenIsValid(token, topicKey))
                {
                    _logger.Error("'aeg-sas-key' value did not match configured value!");
                    return false;
                }

                _logger.Debug($"'aeg-sas-token' header is valid");
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

            var encodedResource = HttpUtility.UrlEncode(decodedResource);
            var encodedExpiration = HttpUtility.UrlEncode(decodedExpiration);

            var unsignedSas = $"r={encodedResource}&e={encodedExpiration}";

            using (var hmac = new HMACSHA256(Convert.FromBase64String(key)))
            {
                var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedSas)));
                var encodedComputedSignature = HttpUtility.UrlEncode(signature);

                if (encodedSignature == signature)
                {
                    return true;
                }

                _logger.Warning($"{encodedComputedSignature} != {signature}");
                return false;
            }
        }
    }
}
