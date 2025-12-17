using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Middleware;

[Trait("Category", "unit")]
public class SasKeyValidatorNoAuthTests : SasKeyValidatorTestBase
{
    [Fact]
    public void GivenNoAuthHeader_WhenValidated_ThenReturnsFalse()
    {
        var headers = new HeaderDictionary();

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenUnrelatedHeaders_WhenValidated_ThenReturnsFalse()
    {
        var headers = new HeaderDictionary
        {
            { "Content-Type", "application/json" },
            { "Accept", "*/*" },
        };

        var result = Validator.IsValid(headers, ValidTopicKey);

        result.ShouldBeFalse();
    }
}
