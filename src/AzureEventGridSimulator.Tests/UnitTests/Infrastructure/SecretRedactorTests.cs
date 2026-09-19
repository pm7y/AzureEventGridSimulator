using AzureEventGridSimulator.Infrastructure;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Infrastructure;

[Trait("Category", "unit")]
public class SecretRedactorTests
{
    [Theory]
    [InlineData("topics:0:key")]
    [InlineData("topics:0:KEY")]
    [InlineData("topics:0:serviceBusSharedAccessKey")]
    [InlineData("topics:0:subscribers:serviceBus:0:sharedAccessKey")]
    [InlineData("topics:0:subscribers:eventHub:0:connectionString")]
    [InlineData("topics:0:eventHubConnectionString")]
    [InlineData("topics:0:storageQueueConnectionString")]
    [InlineData("Kestrel:Certificates:Default:Password")]
    [InlineData("ConnectionStrings:Default:ConnectionString")]
    [InlineData("password")]
    public void GivenAKeyThatNamesASecret_WhenChecked_ThenItIsSecret(string key)
    {
        SecretRedactor.IsSecretKey(key).ShouldBeTrue();
    }

    [Theory]
    [InlineData("topics:0:name")]
    [InlineData("topics:0:port")]
    [InlineData("topics:0:serviceBusSharedAccessKeyName")]
    [InlineData("topics:0:subscribers:eventHub:0:eventHubName")]
    [InlineData("topics:0:subscribers:http:0:endpoint")]
    [InlineData("Kestrel:Certificates:Default:Path")]
    [InlineData("key:0:name")]
    [InlineData("Serilog:MinimumLevel:Default")]
    [InlineData("topics:0:subscribers:http:0:filter:advancedFilters:0:key")]
    [InlineData("topics:1:subscribers:serviceBus:2:filter:AdvancedFilters:3:Key")]
    [InlineData("")]
    public void GivenAKeyThatDoesNotNameASecret_WhenChecked_ThenItIsNotSecret(string key)
    {
        SecretRedactor.IsSecretKey(key).ShouldBeFalse();
    }

    [Fact]
    public void GivenConnectionStringWithKeyLast_WhenRedacted_ThenOnlyTheKeyIsMasked()
    {
        var result = SecretRedactor.RedactConnectionString(
            "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=abc123="
        );

        result.ShouldBe(
            "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=***REDACTED***"
        );
    }

    [Fact]
    public void GivenConnectionStringWithKeyFirst_WhenRedacted_ThenTheKeyIsMasked()
    {
        var result = SecretRedactor.RedactConnectionString(
            "SharedAccessKey=abc123=;Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=Root;EntityPath=hub"
        );

        result.ShouldBe(
            "SharedAccessKey=***REDACTED***;Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=Root;EntityPath=hub"
        );
    }

    [Fact]
    public void GivenConnectionStringWithSasToken_WhenRedacted_ThenTheWholeTokenIsMasked()
    {
        var result = SecretRedactor.RedactConnectionString(
            "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessSignature=SharedAccessSignature sr=sb%3a%2f%2fns.servicebus.windows.net%2f&sig=c2lnbmF0dXJl%3d&se=1900000000&skn=Root"
        );

        result.ShouldBe(
            "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessSignature=***REDACTED***"
        );
    }

    [Fact]
    public void GivenStorageConnectionString_WhenRedacted_ThenTheAccountKeyIsMasked()
    {
        var result = SecretRedactor.RedactConnectionString(
            "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=a2V5a2V5a2V5==;EndpointSuffix=core.windows.net"
        );

        result.ShouldBe(
            "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=***REDACTED***;EndpointSuffix=core.windows.net"
        );
    }

    [Fact]
    public void GivenSecretNamesInAnyCaseOrWithSpaces_WhenRedacted_ThenTheyAreStillMasked()
    {
        var result = SecretRedactor.RedactConnectionString(
            "endpoint=sb://ns/; sharedaccesskey=abc123;ACCOUNTKEY=def456"
        );

        result.ShouldBe(
            "endpoint=sb://ns/; sharedaccesskey=***REDACTED***;ACCOUNTKEY=***REDACTED***"
        );
    }

    [Fact]
    public void GivenConnectionStringWithoutSecrets_WhenRedacted_ThenItIsUnchanged()
    {
        const string connectionString =
            "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;UseDevelopmentEmulator=true;";

        SecretRedactor.RedactConnectionString(connectionString).ShouldBe(connectionString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GivenNoConnectionString_WhenRedacted_ThenReturnsItUnchanged(
        string? connectionString
    )
    {
        SecretRedactor.RedactConnectionString(connectionString).ShouldBe(connectionString);
    }
}
