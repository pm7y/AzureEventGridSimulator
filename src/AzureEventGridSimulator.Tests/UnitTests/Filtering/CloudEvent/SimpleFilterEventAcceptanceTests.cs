namespace AzureEventGridSimulator.Tests.UnitTests.Filtering.CloudEvent;

using AzureEventGridSimulator.Domain.Entities;
using Xunit;

[Trait("Category", "unit")]
[Trait("Type", "CloudEvent")]
public class SimpleFilterEventAcceptanceTests : BaseSimpleFilterEventAcceptanceTests<EventGridEvent>
{
}
