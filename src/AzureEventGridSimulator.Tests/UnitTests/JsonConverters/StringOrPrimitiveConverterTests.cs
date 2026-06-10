using System.Globalization;
using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.JsonConverters;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.JsonConverters;

[Trait("Category", "unit")]
public class StringOrPrimitiveConverterTests
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new StringOrPrimitiveConverter());
        return options;
    }

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("42", "42")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    public void GivenPrimitiveToken_WhenDeserialized_ThenCoercedToString(
        string json,
        string expected
    )
    {
        JsonSerializer.Deserialize<string>(json, _options).ShouldBe(expected);
    }

    [Fact]
    public void GivenNullToken_WhenDeserialized_ThenNull()
    {
        JsonSerializer.Deserialize<string>("null", _options).ShouldBeNull();
    }

    [Fact]
    public void GivenObjectToken_WhenDeserialized_ThenThrowsJsonException()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<string>("{}", _options));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void GivenFractionalNumber_WhenDeserializedUnderNonInvariantCulture_ThenUsesInvariantFormat(
        string cultureName
    )
    {
        // Under de-DE a culture-sensitive ToString() would produce "3,5"
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            JsonSerializer.Deserialize<string>("3.5", _options).ShouldBe("3.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
