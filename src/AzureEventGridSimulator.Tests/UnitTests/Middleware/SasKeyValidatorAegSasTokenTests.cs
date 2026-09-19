using System.Web;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Middleware;

[Trait("Category", "unit")]
public class SasKeyValidatorAegSasTokenTests : SasKeyValidatorTestBase
{
    [Fact]
    public void GivenValidAegSasToken_WhenValidated_ThenReturnsTrue()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Fact]
    public void GivenExpiredAegSasToken_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(-5)
        );
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.TokenExpired));
    }

    [Fact]
    public void GivenAegSasTokenWithWrongSignature_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(
            "WrongKey123456789012345=",
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.SignatureMismatch));
    }

    [Fact]
    public void GivenInvalidAegSasToken_WhenValidated_ThenLogsError()
    {
        var token = GenerateValidSasToken(
            "WrongKey123456789012345=",
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.Validate(headers, ValidTopicKey);

        Logger
            .Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o != null && string.Concat(o).Contains("aeg-sas-token")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenAegSasTokenWithWrongSignature_WhenValidated_ThenExpectedSignatureIsNotLogged()
    {
        // The expected signature is the HMAC of the caller's own 'r' and 'e', so logging it
        // would hand anyone who can read the logs a valid token
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
        var expectedSignature = HttpUtility
            .ParseQueryString(GenerateValidSasToken(ValidTopicKey, "http://localhost", expiry))
            .Get("s")
            .ShouldNotBeNull();
        var token = $"r=http%3A%2F%2Flocalhost&e={expiry.ToUnixTimeSeconds()}&s=not-the-signature";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.SignatureMismatch));
        Logger
            .DidNotReceive()
            .Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o != null && string.Concat(o).Contains(expectedSignature)),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenAegSasTokenWithWrongSignature_WhenValidated_ThenLogsResourceAndExpiryAtDebug()
    {
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var token = $"r=http%3A%2F%2Flocalhost%2Fa%0Ab&e={expiry}&s=not-the-signature";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.Validate(headers, ValidTopicKey);

        // The resource comes from the caller, so its control characters are escaped
        Logger
            .Received()
            .Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains($"http://localhost/a\\nb\\n{expiry}")
                    && !string.Concat(o).Contains("http://localhost/a\nb")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenAegSasTokenWithDifferentCase_WhenValidated_ThenHeaderIsFoundCaseInsensitive()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { "AEG-SAS-TOKEN", token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Fact]
    public void GivenTokenExpiringExactlyNow_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(ValidTopicKey, "http://localhost", DateTimeOffset.UtcNow);
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.TokenExpired));
    }

    [Fact]
    public void GivenTokenWithInvalidExpirationFormat_WhenValidated_ThenReturnsFalse()
    {
        // A non-numeric 'e' is reported as TokenExpired, not InvalidTokenFormat. This pins
        // today's behaviour: changing the reason would change the 401 message users see.
        const string token = "r=http%3A%2F%2Flocalhost&e=not-a-valid-date&s=somesignature";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.TokenExpired));
    }

    [Theory]
    [InlineData("99999999999999")]
    [InlineData("-99999999999999")]
    [InlineData("253402300800")] // one second after DateTimeOffset.MaxValue
    [InlineData("-62135596801")] // one second before DateTimeOffset.MinValue
    public void GivenTokenWithExpiryOutsideTheDateRange_WhenValidated_ThenFailsWithInvalidTokenFormat(
        string expiry
    )
    {
        // Parses as a long but can't be a date (e.g. an expiry in milliseconds, not seconds)
        var token = $"r=http%3A%2F%2Flocalhost&e={expiry}&s=somesignature";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.InvalidTokenFormat));
    }

    [Fact]
    public void GivenTokenExpiringAtTheLastRepresentableSecond_WhenValidated_ThenReturnsTrue()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.MaxValue
        );
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Theory]
    [InlineData("e=4102444800&s=somesignature")]
    [InlineData("r=http%3A%2F%2Flocalhost&s=somesignature")]
    [InlineData("r=http%3A%2F%2Flocalhost&e=4102444800")]
    [InlineData("r=&e=4102444800&s=somesignature")]
    [InlineData("")]
    public void GivenTokenMissingResourceExpiryOrSignature_WhenValidated_ThenFailsWithInvalidTokenFormat(
        string token
    )
    {
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.InvalidTokenFormat));
    }

    [Fact]
    public void GivenTopicKeyThatIsNotBase64_WhenTokenValidated_ThenFailsWithInvalidBase64()
    {
        // The topic key is only base64-decoded to check a token's signature; an aeg-sas-key
        // is compared as a plain string
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.Validate(headers, "not-a-base64-key!");

        result.ShouldBe(Failed(SasValidationFailureReason.InvalidBase64));
    }

    [Fact]
    public void GivenTokenExpiringAtTheCurrentSecond_WhenValidated_ThenFailsWithTokenExpired()
    {
        // A fixed, whole-second clock, so the token's expiry is exactly 'now'
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var validator = new SasKeyValidator(new FakeTimeProvider(now), Logger);
        var token = GenerateValidSasToken(ValidTopicKey, "http://localhost", now);
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.TokenExpired));
    }

    [Fact]
    public void GivenTokenExpiringOneSecondFromNow_WhenValidated_ThenReturnsTrue()
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var validator = new SasKeyValidator(new FakeTimeProvider(now), Logger);
        var token = GenerateValidSasToken(ValidTopicKey, "http://localhost", now.AddSeconds(1));
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Theory]
    [InlineData("fake\nsignature", "fake\\nsignature")]
    [InlineData("fake\rsignature", "fake\\rsignature")]
    [InlineData("fake\tsignature", "fake\\tsignature")]
    [InlineData("fake\n\r\t\0signature", "fake\\n\\r\\t\\0signature")]
    [InlineData("fake\u007Fsignature", "fake\\x7Fsignature")] // DEL (ASCII 127)
    public void GivenTokenWithSignatureContainingControlCharacters_WhenValidated_ThenSanitizesThemInLog(
        string maliciousSignature,
        string sanitizedSignature
    )
    {
        var token =
            $"r=http%3A%2F%2Flocalhost&e={DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}&s={maliciousSignature}";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.Validate(headers, ValidTopicKey);

        // The signature comes from the caller, so its control characters are escaped
        Logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains(sanitizedSignature)
                    && !string.Concat(o).Contains(maliciousSignature)
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }
}
