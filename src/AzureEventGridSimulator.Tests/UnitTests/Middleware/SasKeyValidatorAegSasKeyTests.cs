using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Infrastructure.Middleware;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Middleware;

[Trait("Category", "unit")]
public class SasKeyValidatorAegSasKeyTests : SasKeyValidatorTestBase
{
    [Fact]
    public void GivenValidAegSasKey_WhenValidated_ThenReturnsTrue()
    {
        var headers = new HeaderDictionary { { Constants.AegSasKeyHeader, ValidTopicKey } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Fact]
    public void GivenInvalidAegSasKey_WhenValidated_ThenReturnsFalse()
    {
        var headers = new HeaderDictionary { { Constants.AegSasKeyHeader, "wrong-key" } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.KeyMismatch));
    }

    [Fact]
    public void GivenInvalidAegSasKey_WhenValidated_ThenLogsError()
    {
        var headers = new HeaderDictionary { { Constants.AegSasKeyHeader, "wrong-key" } };

        Validator.Validate(headers, ValidTopicKey);

        Logger
            .Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o != null && string.Concat(o).Contains("aeg-sas-key")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void GivenAegSasKeyWithDifferentCase_WhenValidated_ThenHeaderIsFoundCaseInsensitive()
    {
        var headers = new HeaderDictionary { { "AEG-SAS-KEY", ValidTopicKey } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }

    [Fact]
    public void GivenEmptyAegSasKey_WhenValidated_ThenFailsWithEmptyKey()
    {
        // EmptyKey is the one failure the middleware answers with a 400 rather than a 401
        var headers = new HeaderDictionary { { Constants.AegSasKeyHeader, string.Empty } };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Failed(SasValidationFailureReason.EmptyKey));
    }

    [Fact]
    public void GivenValidAegSasKeyAndBadAuthorizationHeader_WhenValidated_ThenAegSasKeyWins()
    {
        // aeg-sas-key is checked first; the other auth headers aren't looked at when it's present
        var headers = new HeaderDictionary
        {
            { Constants.AegSasKeyHeader, ValidTopicKey },
            { HeaderNames.Authorization, "Basic abc" },
        };

        var result = Validator.Validate(headers, ValidTopicKey);

        result.ShouldBe(Valid);
    }
}
