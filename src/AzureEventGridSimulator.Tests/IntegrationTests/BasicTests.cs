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
    public async Task GivenAValidCloudEvent_WhenPublished_ThenItShouldBeAccepted()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost:60101")
        });

        client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "TheLocal+DevelopmentKey=");
        client.DefaultRequestHeaders.Add(Constants.AegEventTypeHeader, Constants.NotificationEventType);

        var data = new BinaryData(Encoding.UTF8.GetBytes("##This is treated as binary data##"));

        var testEvent = new Domain.Entities.CloudEventGridEvent()
        {
            Data_Base64 = Convert.ToBase64String(data),
            Id = "1232",
            Source = "https://awesomesource.com/somestuff",
            Type = "The.Event.Type",
            Time = DateTimeOffset.UtcNow,
            DataSchema = "https://awesomeschema.com/someuri",
            DataContentType = "application/json",
            Subject = "/the/subject",
        };

        var json = JsonConvert.SerializeObject(new[] { testEvent }, Formatting.Indented);

        // Act
        var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/events/cloudevent", jsonContent);

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
