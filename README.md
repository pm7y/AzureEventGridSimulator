# Azure Event Grid Simulator

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/pmcilreavy/AzureEventGridSimulator/ci.yml)
![GitHub contributors](https://img.shields.io/github/contributors-anon/pmcilreavy/AzureEventGridSimulator)
![GitHub tag (latest SemVer)](https://img.shields.io/github/v/tag/pmcilreavy/AzureEventGridSimulator?label=latest)
![GitHub all releases](https://img.shields.io/github/downloads/pmcilreavy/AzureEventGridSimulator/total)
![Docker Pulls](https://img.shields.io/docker/pulls/pmcilreavy/azureeventgridsimulator)
![NuGet Version](https://img.shields.io/nuget/v/AzureEventGridSimulator)

A simulator that provides HTTPS endpoints to mimic [Azure Event Grid](https://azure.microsoft.com/en-au/services/event-grid/) topics and subscribers. Compatible with the `Microsoft.Azure.EventGrid` client library and supports both EventGrid and CloudEvents v1.0 schemas.

> **Note:** This simulator is intended for **local development and testing only**.

## Installation

### .NET Tool (Recommended)

```bash
# Global install
dotnet tool install -g AzureEventGridSimulator
azure-eventgrid-simulator

# Or local install
dotnet new tool-manifest
dotnet tool install AzureEventGridSimulator
dotnet tool run azure-eventgrid-simulator
```

### Docker

```bash
docker pull pmcilreavy/azureeventgridsimulator:latest
```

### Binary

Download from [GitHub Releases](https://github.com/pmcilreavy/AzureEventGridSimulator/releases).

## Quick Start

Create an `appsettings.json` file:

```json
{
  "topics": [
    {
      "name": "MyTopic",
      "port": 60101,
      "key": "TheLocal+DevelopmentKey=",
      "subscribers": [
        {
          "name": "MySubscriber",
          "endpoint": "http://localhost:7071/api/MyFunction",
          "disableValidation": true
        }
      ]
    }
  ]
}
```

Run the simulator, then post events:

```bash
curl -k -X POST "https://localhost:60101/api/events?api-version=2018-01-01" \
  -H "Content-Type: application/json" \
  -H "aeg-sas-key: TheLocal+DevelopmentKey=" \
  -d '[{"id":"1","subject":"/test","eventType":"Test","eventTime":"2024-01-01T00:00:00Z","data":{"message":"Hello"},"dataVersion":"1"}]'
```

## Dashboard

Access the built-in dashboard at `https://localhost:<port>/dashboard` to view event history and delivery status.

## Documentation

For detailed documentation, see the **[Wiki](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki)**:

- [Configuration](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Configuration) - Topics, subscribers, and app settings
- [HTTP Subscribers](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/HTTP-Subscribers) - Webhook configuration
- [Service Bus Subscribers](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Service-Bus-Subscribers) - Azure Service Bus
- [Storage Queue Subscribers](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Storage-Queue-Subscribers) - Azure Storage Queues
- [Event Hub Subscribers](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Event-Hub-Subscribers) - Azure Event Hubs
- [Filtering](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Filtering) - Event filtering
- [Retry and Dead-Letter](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Retry-and-Dead-Letter) - Retry policies
- [Dashboard](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Dashboard) - Web-based monitoring
- [Docker](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Docker) - Container deployment
- [Schema Support](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Schema-Support) - EventGrid and CloudEvents
- [Architecture](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Architecture) - System design and internals

## Contributing

See the [Architecture](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki/Architecture) page for system design details and the [Wiki](https://github.com/pmcilreavy/AzureEventGridSimulator/wiki) for development guidelines.
