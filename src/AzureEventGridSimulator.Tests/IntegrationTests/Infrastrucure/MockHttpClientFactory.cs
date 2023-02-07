namespace AzureEventGridSimulator.Tests.IntegrationTests.Infrastrucure;

using System.Net.Http;
using RichardSzalay.MockHttp;

public sealed class MockHttpClientFactory : IHttpClientFactory
{
    private readonly MockHttpMessageHandler _mockHttpMessageHandler;

    public MockHttpClientFactory(MockHttpMessageHandler mockHttpMessageHandler)
    {
        _mockHttpMessageHandler = mockHttpMessageHandler;
    }

    public HttpClient CreateClient(string name)
    {
        return _mockHttpMessageHandler.ToHttpClient();
    }
}
