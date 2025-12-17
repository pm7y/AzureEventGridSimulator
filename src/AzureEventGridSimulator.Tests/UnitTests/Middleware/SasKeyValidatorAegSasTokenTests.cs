using System;
using AzureEventGridSimulator.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
            DateTime.UtcNow.AddMinutes(5)
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
            DateTime.UtcNow.AddMinutes(-5)
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
            DateTime.UtcNow.AddMinutes(5)
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
            DateTime.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        Validator.IsValid(headers, ValidTopicKey);

        Logger
            .Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains("aeg-sas-token")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public void GivenAegSasTokenWithDifferentCase_WhenValidated_ThenHeaderIsFoundCaseInsensitive()
    {
        var token = GenerateValidSasToken(
            ValidTopicKey,
            "http://localhost",
            DateTime.UtcNow.AddMinutes(5)
        );
        var headers = new HeaderDictionary { { "AEG-SAS-TOKEN", token } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
    }

    [Fact]
    public void GivenTokenExpiringExactlyNow_WhenValidated_ThenReturnsFalse()
    {
        var token = GenerateValidSasToken(ValidTopicKey, "http://localhost", DateTime.UtcNow);
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenTokenWithInvalidExpirationFormat_WhenValidated_ThenReturnsFalse()
    {
        var token = "r=http%3A%2F%2Flocalhost&e=not-a-valid-date&s=somesignature";
        var headers = new HeaderDictionary { { Constants.AegSasTokenHeader, token } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }
}
