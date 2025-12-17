using System;
using AzureEventGridSimulator.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
            DateTime.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
    }

    [Fact]
    public void GivenExpiredAuthorizationHeader_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTime.UtcNow.AddMinutes(-5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenAuthorizationHeaderWithWrongSignature_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(
            "WrongKey123456789012345=",
            "http://localhost",
            DateTime.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenInvalidAuthorizationHeader_WhenValidated_ThenLogsError()
    {
        var token = GenerateValidSasToken(
            "WrongKey123456789012345=",
            "http://localhost",
            DateTime.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, $"{Constants.SasAuthorizationType} {token}" },
        };

        Validator.IsValid(headers, ValidTopicKey);

        Logger
            .Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains("SharedAccessSignature")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public void GivenNonSasAuthorizationHeader_WhenValidated_ThenReturnsTrue()
    {
        var headers = new HeaderDictionary
        {
            { HeaderNames.Authorization, "Bearer some-jwt-token" },
        };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
    }

    [Fact]
    public void GivenAuthorizationHeaderWithDifferentCase_WhenValidated_ThenHeaderIsFoundCaseInsensitive()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTime.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary
        {
            { "AUTHORIZATION", $"{Constants.SasAuthorizationType} {token}" },
        };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
    }
}
