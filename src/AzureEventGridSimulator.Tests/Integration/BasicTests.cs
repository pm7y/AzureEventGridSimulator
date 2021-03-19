﻿using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.Integration
{
    public class BasicTests
        : IClassFixture<TestContextFixture>
    {
        private readonly TestContextFixture _factory;

        public BasicTests(TestContextFixture factory)
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

            var testEvent = new Azure.Messaging.EventGrid.EventGridEvent("subject", "eventType", "1.0", new { Blah = 1 });
            var json = JsonConvert.SerializeObject(new[] { testEvent }, Formatting.Indented);

            // Act
            var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/events", jsonContent);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }
}
