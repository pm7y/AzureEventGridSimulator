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
    [InlineData("-5", "-5")]
    [InlineData("9223372036854775807", "9223372036854775807")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    public void GivenPrimitiveToken_WhenDeserialized_ThenCoercedToString(
        string json,
        string expected
    )
    {
        JsonSerializer.Deserialize<string>(json, _options).ShouldBe(expected);
    }

    // These rows record the current lossy behaviour rather than endorse it: any number that
    // doesn't parse as an Int64 is read as a double and re-formatted, so its original text is lost.
    [Theory]
    [InlineData("1.0", "1")]
    [InlineData("1.10", "1.1")]
    [InlineData("1e3", "1000")]
    [InlineData("-0.0", "-0")]
    [InlineData("12345678901234567890", "1.2345678901234567E+19")]
    public void GivenNonInt64Number_WhenDeserialized_ThenNormalisedViaDouble(
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
