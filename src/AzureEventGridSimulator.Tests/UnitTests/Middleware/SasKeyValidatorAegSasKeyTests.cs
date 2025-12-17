using AzureEventGridSimulator.Domain;
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

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
    }

    [Fact]
    public void GivenInvalidAegSasKey_WhenValidated_ThenReturnsFalse()
    {
        var headers = new HeaderDictionary { { Constants.AegSasKeyHeader, "wrong-key" } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenInvalidAegSasKey_WhenValidated_ThenLogsError()
    {
        var headers = new HeaderDictionary { { Constants.AegSasKeyHeader, "wrong-key" } };

        Validator.IsValid(headers, ValidTopicKey);

        Logger
            .Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains("aeg-sas-key")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public void GivenAegSasKeyWithDifferentCase_WhenValidated_ThenHeaderIsFoundCaseInsensitive()
    {
        var headers = new HeaderDictionary { { "AEG-SAS-KEY", ValidTopicKey } };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeTrue();
    }
}
