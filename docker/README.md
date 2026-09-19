# Azure Event Grid Simulator - Docker Guide

The Azure Event Grid Simulator provides a local development environment that mimics Azure Event Grid functionality. This guide covers everything you need to run the simulator using Docker.

**GitHub Repository**: [https://github.com/pm7y/AzureEventGridSimulator](https://github.com/pm7y/AzureEventGridSimulator)

**Docker Hub**: [pmcilreavy/azureeventgridsimulator](https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator)

**Supported Platforms**: `linux/amd64`, `linux/arm64`

---

## Quick Start

### 1. Generate a Development Certificate

The simulator requires HTTPS. Generate a development certificate:

```bash
# Trust the certificate (one-time setup)
dotnet dev-certs https --trust

# Export the certificate (dev-certs won't create the folder)
mkdir -p certs
dotnet dev-certs https \
  --export-path ./certs/eventgrid.pfx \
  --password password123

# The container runs as UID 1654 and dev-certs exports the file as 0600
chmod a+r ./certs/eventgrid.pfx
```

### 2. Create a Configuration File

Create `config/appsettings.json`:

```json
{
  "topics": [
    {
      "name": "my-topic",
      "port": 60101,
      "key": "TheLocal+DevelopmentKey=",
      "subscribers": {
        "http": [
          {
            "name": "webhook-subscriber",
            "endpoint": "https://webhook.site/your-unique-id",
            "disableValidation": true
          }
        ]
      }
    }
  ]
}
```

### 3. Run the Container

```bash
docker run -d \
  --name eventgrid \
  -p 60101:60101 \
  -v $(pwd)/certs:/certs:ro \
  -v $(pwd)/config:/config:ro \
  -e ASPNETCORE_Kestrel__Certificates__Default__Path=/certs/eventgrid.pfx \
  -e ASPNETCORE_Kestrel__Certificates__Default__Password=password123 \
  -e AEGS_ConfigFile=/config/appsettings.json \
  pmcilreavy/azureeventgridsimulator:latest
```

### 4. Send a Test Event

```bash
curl -k \
  -H "Content-Type: application/json" \
  -H "aeg-sas-key: TheLocal+DevelopmentKey=" \
  -X POST "https://localhost:60101/api/events?api-version=2018-01-01" \
  -d '[{
    "id": "test-event-1",
    "subject": "/orders/12345",
    "eventType": "Order.Created",
    "eventTime": "2025-01-15T10:00:00Z",
    "data": {
      "orderId": "12345",
      "customerId": "cust-789",
      "total": 99.99
    },
    "dataVersion": "1.0"
  }]'
```

---

## Configuration

### Configuration Methods

The simulator can be configured through multiple methods (in order of precedence):

1. **Command line arguments**
2. **Environment variables** (prefixed with `AEGS_`)
3. **Custom config file** (via `AEGS_ConfigFile`)
4. **appsettings.{Environment}.json**
5. **appsettings.json**

### Using a Config File

Mount your configuration file and set the `AEGS_ConfigFile` environment variable:

```bash
docker run -d \
  -v /path/to/config:/config:ro \
  -e AEGS_ConfigFile=/config/appsettings.json \
  pmcilreavy/azureeventgridsimulator:latest
```

### Using Environment Variables

Configure topics and subscribers directly via environment variables. Use double underscores (`__`) for nested properties:

```bash
docker run -d \
  -e AEGS_Topics__0__name=my-topic \
  -e AEGS_Topics__0__port=60101 \
  -e AEGS_Topics__0__key=MySecretKey= \
  -e AEGS_Topics__0__subscribers__http__0__name=webhook \
  -e AEGS_Topics__0__subscribers__http__0__endpoint=https://example.com/webhook \
  -e AEGS_Topics__0__subscribers__http__0__disableValidation=true \
  pmcilreavy/azureeventgridsimulator:latest
```

---

## Topic Configuration

Each topic listens on its own port and can have multiple subscribers.

| Property | Required | Description |
|----------|----------|-------------|
| `name` | Yes | Topic name (letters, numbers, dashes only) |
| `port` | Yes | Port number the topic listens on |
| `key` | No | SAS key for authentication (null = no validation) |
| `disabled` | No | Set to `true` to disable the topic |
| `inputSchema` | No | `EventGridSchema` or `CloudEventV1_0` (auto-detect if null) |
| `outputSchema` | No | Schema for delivery to subscribers |
| `serviceBusConnectionString` | No | Default connection string for Service Bus subscribers |
| `storageQueueConnectionString` | No | Default connection string for Storage Queue subscribers |
| `eventHubConnectionString` | No | Default connection string for Event Hub subscribers |

### Example: Multiple Topics

```json
{
  "topics": [
    {
      "name": "orders-topic",
      "port": 60101,
      "key": "OrdersTopicKey=",
      "inputSchema": "EventGridSchema",
      "subscribers": { }
    },
    {
      "name": "notifications-topic",
      "port": 60102,
      "key": "NotificationsKey=",
      "inputSchema": "CloudEventV1_0",
      "subscribers": { }
    }
  ]
}
```

---

## Subscribers

A topic can deliver to four subscriber types, each in its own array under `subscribers`: `http`, `serviceBus`, `storageQueue` and `eventHub`. This page has one example of each; the wiki has the full reference.

Every subscriber type accepts these settings:

| Property | Required | Description |
|----------|----------|-------------|
| `name` | Yes | Subscriber name (letters, numbers, dashes only; unique within the topic) |
| `disabled` | No | Set to `true` to disable the subscriber |
| `deliverySchema` | No | `EventGridSchema` or `CloudEventV1_0` (defaults to the topic's `outputSchema`, then the schema the event arrived in) |
| `filter` | No | Event filtering rules, see [Filtering](https://github.com/pm7y/AzureEventGridSimulator/wiki/Filtering) |
| `retryPolicy` | No | Retry settings, see [Retry and Dead-Letter](https://github.com/pm7y/AzureEventGridSimulator/wiki/Retry-and-Dead-Letter) |
| `deadLetter` | No | Where undeliverable events are written, see [Retry and Dead-Letter](https://github.com/pm7y/AzureEventGridSimulator/wiki/Retry-and-Dead-Letter) |

### HTTP Webhook

Needs an `endpoint`. Set `disableValidation` to `true` to skip the subscription validation handshake. Reference: [HTTP Subscribers](https://github.com/pm7y/AzureEventGridSimulator/wiki/HTTP-Subscribers).

```json
{
  "http": [
    {
      "name": "order-processor",
      "endpoint": "https://myapp.local/api/events",
      "disableValidation": true,
      "filter": { "includedEventTypes": ["Order.Created", "Order.Updated"] }
    }
  ]
}
```

### Service Bus

Needs a `connectionString` (or `namespace`, `sharedAccessKeyName` and `sharedAccessKey`, or the topic's `serviceBusConnectionString`) and either a `queue` or a `topic`. Reference: [Service Bus Subscribers](https://github.com/pm7y/AzureEventGridSimulator/wiki/Service-Bus-Subscribers).

```json
{
  "serviceBus": [
    {
      "name": "orders-queue-subscriber",
      "connectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=...",
      "queue": "orders-queue",
      "properties": {
        "EventType": { "type": "dynamic", "value": "EventType" },
        "CustomerId": { "type": "dynamic", "value": "data.customerId" },
        "Source": { "type": "static", "value": "EventGridSimulator" }
      }
    }
  ]
}
```

`properties` (Service Bus and Event Hub) become application properties on each message. A `static` value is used as-is. A `dynamic` value is a path into the event: `Id`, `Subject`, `EventType`, `EventTime`, `DataVersion`, `Source`, `Topic`, or `data.propertyName` / `data.nested.property`.

### Storage Queue

Needs a `queueName` and a `connectionString` (or the topic's `storageQueueConnectionString`). Reference: [Storage Queue Subscribers](https://github.com/pm7y/AzureEventGridSimulator/wiki/Storage-Queue-Subscribers).

```json
{
  "storageQueue": [
    {
      "name": "audit-queue-subscriber",
      "connectionString": "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=...;EndpointSuffix=core.windows.net",
      "queueName": "audit-events"
    }
  ]
}
```

### Event Hub

Needs an `eventHubName` and a `connectionString` (or `namespace`, `sharedAccessKeyName` and `sharedAccessKey`, or the topic's `eventHubConnectionString`). Reference: [Event Hub Subscribers](https://github.com/pm7y/AzureEventGridSimulator/wiki/Event-Hub-Subscribers).

```json
{
  "eventHub": [
    {
      "name": "orders-eventhub-subscriber",
      "connectionString": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=...",
      "eventHubName": "orders-events",
      "properties": {
        "OrderId": { "type": "dynamic", "value": "data.orderId" }
      }
    }
  ]
}
```

### Retry, Dead-Letter and Filtering

Failed deliveries are retried with Azure Event Grid's exponential backoff, and a subscriber's `deadLetter` settings write undeliverable events to JSON files (see *Container User and File Permissions* below to keep them outside the container). Subscribers can also filter on event type, subject and advanced conditions on event fields and data. The settings, schedules, operators and limits are in [Retry and Dead-Letter](https://github.com/pm7y/AzureEventGridSimulator/wiki/Retry-and-Dead-Letter) and [Filtering](https://github.com/pm7y/AzureEventGridSimulator/wiki/Filtering).

---

## Dashboard

The simulator serves a web dashboard showing received events, delivery attempts and rejected requests. It's served on each enabled topic's port, for example `https://localhost:60101/dashboard`, and also on `dashboardPort` if you set one (publish that port too, e.g. `-p 5000:5000`). Turn it off with `-e AEGS_dashboardEnabled=false`. More: [Dashboard](https://github.com/pm7y/AzureEventGridSimulator/wiki/Dashboard).

---

## HTTPS Certificates

The simulator requires HTTPS (matching Azure Event Grid's behavior).

### Option 1: .NET Development Certificate (Recommended)

```bash
# Trust the certificate locally
dotnet dev-certs https --trust

# Export for Docker (dev-certs won't create the folder)
mkdir -p certs
dotnet dev-certs https \
  --export-path ./certs/eventgrid.pfx \
  --password YourSecurePassword123

# The container runs as UID 1654 and dev-certs exports the file as 0600
chmod a+r ./certs/eventgrid.pfx
```

### Option 2: Custom Certificate

Use any PFX certificate:

```bash
docker run -d \
  -v /path/to/certs:/certs:ro \
  -e ASPNETCORE_Kestrel__Certificates__Default__Path=/certs/mycert.pfx \
  -e ASPNETCORE_Kestrel__Certificates__Default__Password=certpassword \
  pmcilreavy/azureeventgridsimulator:latest
```

### Accept Self-Signed Certificates

When subscribers use self-signed certificates, enable acceptance in the simulator:

```bash
-e AEGS_dangerousAcceptAnyServerCertificateValidator=true
```

> **Warning**: Only use this in development environments.

---

## Container User and File Permissions

The image runs as the non-root `app` user (UID `1654`), not as root. Inside the image, the `/app` folder and the default dead-letter folder `/app/dead-letters` belong to that user. Bind mounts keep their host ownership and permissions, so on Linux hosts (Docker Desktop on macOS and Windows usually handles this for you):

- **Certificates and config files** you mount must be readable by UID 1654. A `.pfx` that only your host user can read (mode `0600`) fails to load, and `dotnet dev-certs https --export-path` writes exactly that on Linux and macOS; make it readable, e.g. `chmod a+r certs/eventgrid.pfx` (the Quick Start does this).
- **Dead-letter and log folders** you mount must be writable by UID 1654. For example:

  ```bash
  mkdir -p dead-letters && sudo chown 1654 dead-letters
  docker run ... -v $(pwd)/dead-letters:/app/dead-letters pmcilreavy/azureeventgridsimulator:latest
  ```

  If the folder isn't writable, the dead-letter file is not written and the simulator only logs an error.
- **Alternatively, run as your own user** with `--user "$(id -u):$(id -g)"` (Compose: `user:`). That user can't write to the image's own `/app/dead-letters`, so mount a dead-letter folder as shown above.

Topic ports below 1024 need extra privileges on some container runtimes (Docker Engine 20.10+ allows them), so prefer ports above 1024, as the examples do.

---

## Docker Compose Examples

### Basic Setup with Webhook Subscriber

```yaml
# docker-compose.yml
services:
  eventgrid:
    image: pmcilreavy/azureeventgridsimulator:latest
    ports:
      - "60101:60101"
    volumes:
      - ./certs:/certs:ro
      - ./config:/config:ro
    environment:
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/certs/eventgrid.pfx
      - ASPNETCORE_Kestrel__Certificates__Default__Password=password123
      - AEGS_ConfigFile=/config/appsettings.json
```

### Full Stack with Azurite and Service Bus Emulator

This setup includes:
- Azure Event Grid Simulator
- Azurite (Azure Storage Emulator)
- Azure Service Bus Emulator
- Seq (structured logging UI)

```yaml
# docker-compose.yml
services:
  eventgrid:
    image: pmcilreavy/azureeventgridsimulator:latest
    container_name: eventgrid
    ports:
      - "60101:60101"
      - "60102:60102"
    volumes:
      # on Linux the pfx and config must be readable by UID 1654 (see Container User and File Permissions)
      - ./docker:/aegs:ro
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/aegs/azureEventGridSimulator.pfx
      - ASPNETCORE_Kestrel__Certificates__Default__Password=Y0urSup3rCrypt1cPa55w0rd!
      - TZ=UTC
      - AEGS_ConfigFile=/aegs/appsettings.json
      - AEGS_dangerousAcceptAnyServerCertificateValidator=true
    depends_on:
      - seq
      - azurite
      - servicebus-emulator

  seq:
    image: datalust/seq:latest
    container_name: seq
    ports:
      - "8081:80"
      - "5341:5341"
    environment:
      - ACCEPT_EULA=Y

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:latest
    container_name: azurite
    ports:
      - "10000:10000"  # Blob
      - "10001:10001"  # Queue
      - "10002:10002"  # Table
    command: "azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0"

  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: mssql
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd!

  servicebus-emulator:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    container_name: servicebus-emulator
    ports:
      - "5672:5672"
    volumes:
      - ./docker/servicebus-config.json:/ServiceBus_Emulator/ConfigFiles/Config.json:ro
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd!
      - SQL_SERVER=mssql
    depends_on:
      - mssql
```

**docker/appsettings.json:**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Seq",
        "Args": { "serverUrl": "http://seq:5341" }
      }
    ]
  },
  "topics": [
    {
      "name": "orders-topic",
      "port": 60101,
      "key": "OrdersTopicKey=",
      "subscribers": {
        "http": [
          {
            "name": "order-webhook",
            "endpoint": "https://webhook.site/your-unique-id",
            "disableValidation": true,
            "filter": {
              "includedEventTypes": ["Order.Created"]
            }
          }
        ],
        "serviceBus": [
          {
            "name": "order-queue",
            "connectionString": "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            "queue": "orders",
            "properties": {
              "OrderId": { "type": "dynamic", "value": "data.orderId" }
            }
          }
        ],
        "storageQueue": [
          {
            "name": "order-audit",
            "connectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://azurite:10001/devstoreaccount1;",
            "queueName": "order-audit"
          }
        ]
      }
    },
    {
      "name": "notifications-topic",
      "port": 60102,
      "key": "NotificationsKey=",
      "subscribers": {
        "http": [
          {
            "name": "notification-handler",
            "endpoint": "https://myapp.local/notifications",
            "disableValidation": true
          }
        ]
      }
    }
  ]
}
```

**docker/servicebus-config.json:**

```json
{
  "UserConfig": {
    "Namespaces": [
      {
        "Name": "default",
        "Queues": [
          { "Name": "orders" },
          { "Name": "notifications" }
        ]
      }
    ],
    "Logging": {
      "Type": "Console"
    }
  }
}
```

### Minimal Environment Variables Only

No config file required - configure everything via environment variables:

```yaml
# docker-compose.yml
services:
  eventgrid:
    image: pmcilreavy/azureeventgridsimulator:latest
    ports:
      - "60101:60101"
    volumes:
      - ./certs:/certs:ro
    environment:
      # Certificate
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/certs/eventgrid.pfx
      - ASPNETCORE_Kestrel__Certificates__Default__Password=password123
      # Topic configuration
      - AEGS_Topics__0__name=my-topic
      - AEGS_Topics__0__port=60101
      - AEGS_Topics__0__key=MyTopicKey=
      # HTTP subscriber
      - AEGS_Topics__0__subscribers__http__0__name=webhook
      - AEGS_Topics__0__subscribers__http__0__endpoint=https://webhook.site/your-id
      - AEGS_Topics__0__subscribers__http__0__disableValidation=true
```

---

## Environment Variables Reference

### ASP.NET Core Settings

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Environment name (Development, Production) |
| `ASPNETCORE_Kestrel__Certificates__Default__Path` | Path to HTTPS certificate |
| `ASPNETCORE_Kestrel__Certificates__Default__Password` | Certificate password |

### Simulator Settings (AEGS_ prefix)

| Variable | Description |
|----------|-------------|
| `AEGS_ConfigFile` | Path to configuration JSON file |
| `AEGS_dangerousAcceptAnyServerCertificateValidator` | Accept self-signed subscriber certs |
| `AEGS_dashboardEnabled` | Set to `false` to turn off the dashboard (default: `true`) |
| `AEGS_dashboardPort` | Extra port to serve the dashboard on (publish it too) |
| `AEGS_eventValidationLimits__maximumOverallMessageSizeInBytes` | Maximum request body size (default: `1536000`) |
| `AEGS_eventValidationLimits__maximumEventSizeInBytes` | Maximum size of a single event (default: `1049600`) |
| `AEGS_Topics__[index]__name` | Topic name |
| `AEGS_Topics__[index]__port` | Topic port |
| `AEGS_Topics__[index]__key` | Topic SAS key |
| `AEGS_Topics__[index]__disabled` | Disable topic |
| `AEGS_Topics__[index]__inputSchema` | Input schema |
| `AEGS_Topics__[index]__outputSchema` | Output schema |
| `AEGS_Serilog__MinimumLevel__Default` | Log level (Verbose, Debug, Information, Warning, Error) |

### General Settings

| Variable | Description |
|----------|-------------|
| `TZ` | Timezone (e.g., `UTC`, `America/New_York`, `Europe/London`) |

---

## API Reference

### Send Events

**Endpoint:** `POST https://localhost:{port}/api/events?api-version=2018-01-01`

**Headers:**
- `Content-Type: application/json`
- `aeg-sas-key: {your-topic-key}` (if key is configured)

**Event Grid Schema:**
```json
[
  {
    "id": "unique-event-id",
    "subject": "/orders/12345",
    "eventType": "Order.Created",
    "eventTime": "2025-01-15T10:30:00Z",
    "data": {
      "orderId": "12345",
      "amount": 99.99
    },
    "dataVersion": "1.0"
  }
]
```

**CloudEvents Schema:**
```json
[
  {
    "specversion": "1.0",
    "id": "unique-event-id",
    "source": "/orders",
    "type": "Order.Created",
    "time": "2025-01-15T10:30:00Z",
    "data": {
      "orderId": "12345",
      "amount": 99.99
    }
  }
]
```

### Health Check

**Endpoint:** `GET https://localhost:{port}/api/health`

**Response:** `OK`

---

## Logging with Seq

The simulator integrates with [Seq](https://datalust.co/seq) for structured logging.

Add Seq configuration:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq:5341"
        }
      }
    ]
  }
}
```

Access the Seq UI at `http://localhost:8081` (when using the docker-compose example above).

---

## Troubleshooting

### Certificate Errors

**Problem:** `The remote certificate is invalid`

**Solution:** Ensure your certificate is trusted or enable self-signed cert acceptance:
```bash
-e AEGS_dangerousAcceptAnyServerCertificateValidator=true
```

### Port Already in Use

**Problem:** `Address already in use`

**Solution:** Ensure each topic uses a unique port and that ports are not in use by other applications.

### Permission Denied

**Problem:** The certificate can't be read, or dead-letter or log files aren't written (`Access to the path ... is denied`)

**Solution:** The container runs as UID 1654. See *Container User and File Permissions* above.

### Events Not Delivered

**Checklist:**
1. Verify topic key matches in request header
2. Check subscriber endpoint is reachable from container
3. Review logs for filter mismatches
4. Ensure subscriber's `disableValidation` is `true` for development

### Container Networking

When subscribers run in other containers, use Docker network names:
```json
{
  "endpoint": "https://myapp:5000/webhook"
}
```

For external endpoints, ensure the container can reach them (check DNS, firewalls).

---

## Additional Resources

- **GitHub Repository**: [https://github.com/pm7y/AzureEventGridSimulator](https://github.com/pm7y/AzureEventGridSimulator)
- **Docker Hub**: [https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator](https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator)
- **Azure Event Grid Documentation**: [https://docs.microsoft.com/azure/event-grid/](https://docs.microsoft.com/azure/event-grid/)
- **CloudEvents Specification**: [https://cloudevents.io/](https://cloudevents.io/)
