using AzureEventGridSimulator.Domain;
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

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
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

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
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

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
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

        Validator.IsValid(headers, ValidTopicKey);

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
    public void GivenAegSasTokenWithDifferentCase_WhenValidated_ThenHeaderIsFoundCaseInsensitive()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { "AEG-SAS-TOKEN", token } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
    }

    [Fact]
    public void GivenTokenExpiringExactlyNow_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(ValidTopicKey, "http://localhost", DateTimeOffset.UtcNow);
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenTokenWithInvalidExpirationFormat_WhenValidated_ThenReturnsFalse()
    {
        const string token = "r=http%3A%2F%2Flocalhost&e=not-a-valid-date&s=somesignature";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenTokenWithSignatureContainingNewline_WhenValidated_ThenSanitizesNewlineInLog()
    {
        // Create a token with an invalid signature containing a newline character
        const string maliciousSignature = "fake\nsignature";
        var token =
            $"r=http%3A%2F%2Flocalhost&e={DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}&s={maliciousSignature}";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.IsValid(headers, ValidTopicKey);

        // Verify that the logger was called with the sanitized signature (\\n instead of \n)
        Logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains("fake\\nsignature")
                    && !string.Concat(o).Contains("fake\nsignature")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenTokenWithSignatureContainingCarriageReturn_WhenValidated_ThenSanitizesCarriageReturnInLog()
    {
        // Create a token with an invalid signature containing a carriage return character
        const string maliciousSignature = "fake\rsignature";
        var token =
            $"r=http%3A%2F%2Flocalhost&e={DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}&s={maliciousSignature}";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.IsValid(headers, ValidTopicKey);

        // Verify that the logger was called with the sanitized signature (\\r instead of \r)
        Logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains("fake\\rsignature")
                    && !string.Concat(o).Contains("fake\rsignature")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenTokenWithSignatureContainingTab_WhenValidated_ThenSanitizesTabInLog()
    {
        // Create a token with an invalid signature containing a tab character
        const string maliciousSignature = "fake\tsignature";
        var token =
            $"r=http%3A%2F%2Flocalhost&e={DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}&s={maliciousSignature}";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.IsValid(headers, ValidTopicKey);

        // Verify that the logger was called with the sanitized signature (\\t instead of \t)
        Logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains("fake\\tsignature")
                    && !string.Concat(o).Contains("fake\tsignature")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenTokenWithSignatureContainingMultipleControlCharacters_WhenValidated_ThenSanitizesAllControlCharactersInLog()
    {
        // Create a token with an invalid signature containing multiple control characters
        const string maliciousSignature = "fake\n\r\t\0signature";
        var token =
            $"r=http%3A%2F%2Flocalhost&e={DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}&s={maliciousSignature}";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.IsValid(headers, ValidTopicKey);

        // Verify that all control characters are sanitized
        Logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains("fake\\n\\r\\t\\0signature")
                    && !string.Concat(o).Contains("fake\n\r\t\0signature")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenTokenWithSignatureContainingDelCharacter_WhenValidated_ThenSanitizesDelInLog()
    {
        // Create a token with an invalid signature containing the DEL character (ASCII 127)
        var maliciousSignature = $"fake{(char)127}signature";
        var token =
            $"r=http%3A%2F%2Flocalhost&e={DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}&s={maliciousSignature}";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.IsValid(headers, ValidTopicKey);

        // Verify that the DEL character is sanitized as \x7F
        Logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains("fake\\x7Fsignature")
                    && !string.Concat(o).Contains($"fake{(char)127}signature")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }
}
