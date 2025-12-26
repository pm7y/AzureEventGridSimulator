using System.Security.Cryptography;
using System.Text;
using System.Web;
using AzureEventGridSimulator.Infrastructure.Middleware;
using NSubstitute;

namespace AzureEventGridSimulator.Tests.UnitTests.Middleware;

public abstract class SasKeyValidatorTestBase
{
    protected const string ValidTopicKey = "TheLocal+DevelopmentKey=";
    protected readonly ILogger<SasKeyValidator> Logger;
    protected readonly SasKeyValidator Validator;

    protected SasKeyValidatorTestBase()
    {
        Logger = Substitute.For<ILogger<SasKeyValidator>>();
        Validator = new SasKeyValidator(TimeProvider.System, Logger);
    }

    protected static string GenerateValidSasToken(
        string key,
        string resource,
        DateTimeOffset expiry
    )
    {
        var expiryEpoch = expiry.ToUnixTimeSeconds();
        var stringToSign = $"{resource}\n{expiryEpoch}";

        using var hmac = new HMACSHA256(Convert.FromBase64String(key));
        var signature = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign))
        );

        return $"r={HttpUtility.UrlEncode(resource)}&e={expiryEpoch}&s={HttpUtility.UrlEncode(signature)}";
    }
}
