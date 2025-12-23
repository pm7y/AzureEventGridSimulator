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
        var decodedExpiration = expiry.UtcDateTime.ToString("o");

        var encodedResource = HttpUtility.UrlEncode(resource);
        var encodedExpiration = HttpUtility.UrlEncode(decodedExpiration);

        var unsignedSas = $"r={encodedResource}&e={encodedExpiration}";

        using var hmac = new HMACSHA256(Convert.FromBase64String(key));
        var signature = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedSas))
        );

        var encodedSignature = HttpUtility.UrlEncode(signature);

        return $"r={encodedResource}&e={encodedExpiration}&s={encodedSignature}";
    }
}
