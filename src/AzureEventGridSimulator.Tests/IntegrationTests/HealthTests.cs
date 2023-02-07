namespace AzureEventGridSimulator.Tests.IntegrationTests;

using System;
using System.Net;
using System.Threading.Tasks;
using AzureEventGridSimulator.Tests.IntegrationTests.Infrastrucure;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

[Trait("Category", "integration")]
public class HealthTests : IClassFixture<IntegrationContextFixture>
{
    private readonly IntegrationContextFixture _factory;

    public HealthTests(IntegrationContextFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GivenAHealthRequest_ThenItShouldRespondWithOk()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost:60101")
        });

        // Act
        var response = await client.GetAsync("/api/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("OK");
    }
}
