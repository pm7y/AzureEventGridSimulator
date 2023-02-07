using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
/// These test use a WebApplicationFactory based instance of the simulator
/// and an HttpClient to send send events to the simulator.
/// Note: this is a WIP.
/// </summary>
[Trait("Category", "integration")]
public class BasicTests
    : IClassFixture<IntegrationContextFixture>
{
    private readonly IntegrationContextFixture _factory;

    public BasicTests(IntegrationContextFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GivenAValidEvent_WhenPublished_ThenItShouldBeAccepted()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost:60101")
        });

        client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "TheLocal+DevelopmentKey=");
        client.DefaultRequestHeaders.Add(Constants.AegEventTypeHeader, Constants.NotificationEventType);

        var testEvent = new EventGridEvent("subject", "eventType", "1.0", new { Blah = 1 });
        var json = JsonConvert.SerializeObject(new[] { testEvent }, Formatting.Indented);

        // Act
        var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/events", jsonContent);

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
