using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Infrastructure.Middleware;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Middleware;

[Trait("Category", "unit")]
public class SasKeyValidatorAuthorizationHeaderTests : SasKeyValidatorTestBase
{
    [Fact]
    public void GivenValidAuthorizationHeader_WhenValidated_ThenReturnsTrue()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Fact]
    public void GivenExpiredAuthorizationHeader_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(-5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.TokenExpired));
    }

    [Fact]
    public void GivenAuthorizationHeaderWithWrongSignature_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(
            "WrongKey123456789012345=",
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.SignatureMismatch));
    }

    [Fact]
    public void GivenTokenWhoseResourceContainsTheSchemeName_WhenValidated_ThenReturnsTrue()
    {
        // Only the leading scheme is removed; the same text inside the token must survive
        var token = GenerateValidSasToken(
            ValidTopicKey,
            $"http://localhost/{Constants.SasAuthorizationType}",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Fact]
    public void GivenInvalidAuthorizationHeader_WhenValidated_ThenLogsError()
    {
        var token = GenerateValidSasToken(
            "WrongKey123456789012345=",
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        Validator.Validate(headers, ValidTopicKey);

        Logger
            .Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null && string.Concat(o).Contains("SharedAccessSignature")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenNonSasAuthorizationHeader_WhenValidated_ThenReturnsFalse()
    {
        // Azure Event Grid only supports SharedAccessSignature authorization
        // Bearer, Basic, and other auth types are not supported
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, "Bearer some-jwt-token" },
        };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.BearerTokenInvalid));
    }

    [Fact]
    public void GivenBasicAuthorizationHeader_WhenValidated_ThenFailsWithUnsupportedAuthScheme()
    {
        var headers = new HeaderDictionary { { HeaderNames.Authorization, "Basic abc" } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.UnsupportedAuthScheme));
    }

    [Fact]
    public void GivenAuthorizationHeaderWithDifferentCase_WhenValidated_ThenHeaderIsFoundCaseInsensitive()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { "AUTHORIZATION", $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }
}
