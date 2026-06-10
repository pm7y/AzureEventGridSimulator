using System.Net;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     End-to-end coverage of the subscription validation handshake: the
///     synchronous echo flow (subscriber returns the validation code in the
///     response) and the manual flow (subscriber calls the /validate URL).
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class SubscriptionHandshakeTests(IntegrationContextFixture factory)
{
    private HttpSubscriberSettings GetSubscriber(string name)
    {
        var settings = factory.Services.GetRequiredService<SimulatorSettings>();
        return settings
            .Topics.Single(t => t.Name == "DeliveryFlowTopic")
            .Subscribers.HttpSubscribers.Single(s => s.Name == name);
    }

    private HttpClient CreateTopicClient()
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:60102"),
            }
        );
    }

    private async Task TriggerValidationSweep()
    {
        var mediator = factory.Services.GetRequiredService<IMediator>();
        await mediator.Send(new ValidateAllSubscriptionsCommand());
    }

    [Fact]
    public async Task GivenSubscriberEchoesValidationCode_WhenValidationRuns_ThenSubscriberIsValidated()
    {
        // The capturing handler echoes the validation code back for the
        // echo-handshaker.test endpoint (the synchronous handshake).
        await TriggerValidationSweep();

        GetSubscriber("EchoHandshaker")
            .ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);

        var validationRequest = factory.OutboundHttp.Requests.FirstOrDefault(r =>
            r.Url.StartsWith("https://echo-handshaker.test", StringComparison.OrdinalIgnoreCase)
        );

        var captured = validationRequest.ShouldNotBeNullAnd("no validation event was sent");
        captured.Headers[Constants.AegEventTypeHeader].ShouldBe(Constants.ValidationEventType);
        captured.Body.ShouldContain("Microsoft.EventGrid.SubscriptionValidationEvent");
        captured.Body.ShouldContain("validationCode");
        captured.Body.ShouldContain("validationUrl");
    }

    [Fact]
    public async Task GivenSubscriberThatDoesNotEcho_WhenValidationUrlIsCalledWithCorrectCode_ThenValidated()
    {
        // The manual flow: the subscriber didn't echo synchronously, so it calls
        // the /validate?id=<code> URL from the validation event instead.
        var subscriber = GetSubscriber("ManualHandshaker");
        var client = CreateTopicClient();

        var response = await client.GetAsync($"/validate?id={subscriber.ValidationCode}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("successfully validated");
        subscriber.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
    }

    [Fact]
    public async Task GivenWrongValidationCode_WhenValidationUrlIsCalled_ThenBadRequest()
    {
        var client = CreateTopicClient();

        var response = await client.GetAsync($"/validate?id={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(
            "The validation code was not correct"
        );
    }
}
