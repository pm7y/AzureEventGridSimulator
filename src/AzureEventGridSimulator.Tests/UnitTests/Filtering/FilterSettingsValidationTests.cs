using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

[Trait("Category", "unit")]
public class FilterSettingsValidationTests
{
    private static SimulatorSettings GetValidSimulatorSettings(FilterSetting filter)
    {
        return new SimulatorSettings
        {
            Topics =
            [
                new TopicSettings
                {
                    Key = "TopicKey",
                    Name = "TopicName",
                    Port = 12345,
                    Subscribers = new SubscribersSettings
                    {
                        Http =
                        [
                            new HttpSubscriberSettings
                            {
                                Name = "SubscriberName",
                                Endpoint = "https://example.com/webhook",
                                Filter = filter,
                            },
                        ],
                    },
                },
            ],
        };
    }

    private static AdvancedFilterSetting GetValidAdvancedFilter()
    {
        return new AdvancedFilterSetting
        {
            Key = "key",
            OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.BoolEquals,
            Value = true,
        };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(25)]
    public void TestFilterSettingsValidationWithValidNumberOfAdvancedFilterSettings(byte n)
    {
        Should.NotThrow(() =>
        {
            var filterConfig = new FilterSetting
            {
                AdvancedFilters = new List<AdvancedFilterSetting>(),
            };
            for (byte i = 0; i < n; i++)
            {
                filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
            }

            GetValidSimulatorSettings(filterConfig).Validate();
        });
    }

    [Fact]
    public void TestFilterSettingsValidationWithTooManyAdvancedFilters()
    {
        var filterConfig = new FilterSetting
        {
            AdvancedFilters = new List<AdvancedFilterSetting>(),
        };
        // Azure Event Grid allows up to 25 filters, so 26 should fail
        for (var i = 0; i < 26; i++)
        {
            filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
        }

        var exception = Should.Throw<ArgumentException>(() =>
            GetValidSimulatorSettings(filterConfig).Validate()
        );

        exception.ParamName.ShouldBe(nameof(filterConfig.AdvancedFilters));
        exception.Message.ShouldBe(
            "Advanced filtering is limited to 25 advanced filters per event grid subscription. (Parameter 'AdvancedFilters')"
        );
    }
}
