namespace AzureEventGridSimulator.Tests.UnitTests.Filtering.EventGridEvents;

using AzureEventGridSimulator.Domain.Entities;
using Xunit;

[Trait("Category", "unit")]
[Trait("Type", "EventGridEvent")]
public class SimpleFilterEventAcceptanceTests : BaseSimpleFilterEventAcceptanceTests<EventGridEvent>
{
}
