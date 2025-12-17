# Azure Event Grid Simulator - Docker Guide

The Azure Event Grid Simulator provides a local development environment that mimics Azure Event Grid functionality. This guide covers everything you need to run the simulator using Docker.

**GitHub Repository**: [https://github.com/pmcilreavy/AzureEventGridSimulator](https://github.com/pmcilreavy/AzureEventGridSimulator)

**Docker Hub**: [pmcilreavy/azureeventgridsimulator](https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator)

**Supported Platforms**: `linux/amd64`, `linux/arm64`

---

## Quick Start

### 1. Generate a Development Certificate

The simulator requires HTTPS. Generate a development certificate:

```bash
# Trust the certificate (one-time setup)
dotnet dev-certs https --trust

# Export the certificate
dotnet dev-certs https \
  --export-path ./certs/eventgrid.pfx \
  --password password123
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

## Subscriber Types

The simulator supports three subscriber types: HTTP webhooks, Azure Service Bus, and Azure Storage Queues.

### Subscriber Configuration Format

```json
{
  "subscribers": {
    "http": [ /* HTTP webhook subscribers */ ],
    "serviceBus": [ /* Service Bus queue/topic subscribers */ ],
    "storageQueue": [ /* Storage Queue subscribers */ ]
  }
}
```

### HTTP Webhook Subscribers

| Property | Required | Description |
|----------|----------|-------------|
| `name` | Yes | Subscriber name |
| `endpoint` | Yes | HTTP/HTTPS URL to receive events |
| `disableValidation` | No | Skip subscription validation handshake (default: false) |
| `disabled` | No | Disable the subscriber |
| `deliverySchema` | No | Override delivery schema |
| `filter` | No | Event filtering rules |

```json
{
  "http": [
    {
      "name": "order-processor",
      "endpoint": "https://myapp.local/api/events",
      "disableValidation": true,
      "filter": {
        "includedEventTypes": ["Order.Created", "Order.Updated"]
      }
    }
  ]
}
```

### Service Bus Subscribers

Events are delivered to Azure Service Bus queues or topics.

| Property | Required | Description |
|----------|----------|-------------|
| `name` | Yes | Subscriber name |
| `connectionString` | * | Full Service Bus connection string |
| `namespace` | * | Service Bus namespace (alternative to connectionString) |
| `sharedAccessKeyName` | * | SAS key name (with namespace) |
| `sharedAccessKey` | * | SAS key value (with namespace) |
| `queue` | ** | Queue name |
| `topic` | ** | Topic name |
| `properties` | No | Custom message properties (static or dynamic) |
| `filter` | No | Event filtering rules |

\* Either `connectionString` OR `namespace`+`sharedAccessKeyName`+`sharedAccessKey` required
\** Either `queue` OR `topic` required

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

**Dynamic Property Paths:**
- Top-level event properties: `Id`, `Subject`, `EventType`, `EventTime`, `DataVersion`, `Source`
- Data properties: `data.propertyName`, `data.nested.property`

### Storage Queue Subscribers

Events are delivered to Azure Storage Queues.

| Property | Required | Description |
|----------|----------|-------------|
| `name` | Yes | Subscriber name |
| `connectionString` | * | Storage account connection string (or inherit from topic) |
| `queueName` | Yes | Queue name |
| `disabled` | No | Disable the subscriber |
| `deliverySchema` | No | Override delivery schema |
| `filter` | No | Event filtering rules |

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

---

## Event Filtering

Subscribers can filter events using basic and advanced filters.

### Basic Filtering

```json
{
  "filter": {
    "includedEventTypes": ["Order.Created", "Order.Updated"],
    "subjectBeginsWith": "/orders/",
    "subjectEndsWith": ".json",
    "isSubjectCaseSensitive": false
  }
}
```

### Advanced Filtering

Advanced filters support complex conditions on event data.

**Limits:**
- Maximum 25 filters per subscription
- String values limited to 512 characters
- In/NotIn operators limited to 5 values

**Available Operators:**

| Operator | Value Property | Example Use |
|----------|----------------|-------------|
| `NumberGreaterThan` | `value` | Price > 100 |
| `NumberGreaterThanOrEquals` | `value` | Age >= 18 |
| `NumberLessThan` | `value` | Quantity < 10 |
| `NumberLessThanOrEquals` | `value` | Count <= 5 |
| `NumberIn` | `values` | Status in [1, 2, 3] |
| `NumberNotIn` | `values` | Priority not in [0] |
| `NumberInRange` | `values` | Age in [[18,25], [30,40]] |
| `NumberNotInRange` | `values` | Age not in [[0,17]] |
| `BoolEquals` | `value` | IsActive == true |
| `StringContains` | `values` | Subject contains "test" |
| `StringNotContains` | `values` | Subject doesn't contain "draft" |
| `StringBeginsWith` | `values` | Category starts with "prod" |
| `StringNotBeginsWith` | `values` | Path doesn't start with "/temp" |
| `StringEndsWith` | `values` | FileName ends with ".json" |
| `StringNotEndsWith` | `values` | FileName doesn't end with ".tmp" |
| `StringIn` | `values` | Region in ["us-east", "us-west"] |
| `StringNotIn` | `values` | Env not in ["dev", "test"] |
| `IsNullOrUndefined` | - | OptionalField is null |
| `IsNotNull` | - | RequiredField is not null |

```json
{
  "filter": {
    "advancedFilters": [
      {
        "operatorType": "NumberGreaterThan",
        "key": "data.orderTotal",
        "value": 100
      },
      {
        "operatorType": "StringIn",
        "key": "data.region",
        "values": ["us-east", "us-west", "eu-west"]
      },
      {
        "operatorType": "IsNotNull",
        "key": "data.customerId"
      }
    ]
  }
}
```

---

## HTTPS Certificates

The simulator requires HTTPS (matching Azure Event Grid's behavior).

### Option 1: .NET Development Certificate (Recommended)

```bash
# Trust the certificate locally
dotnet dev-certs https --trust

# Export for Docker
dotnet dev-certs https \
  --export-path ./certs/eventgrid.pfx \
  --password YourSecurePassword123
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

**config/appsettings.json:**

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

- **GitHub Repository**: [https://github.com/pmcilreavy/AzureEventGridSimulator](https://github.com/pmcilreavy/AzureEventGridSimulator)
- **Docker Hub**: [https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator](https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator)
- **Azure Event Grid Documentation**: [https://docs.microsoft.com/azure/event-grid/](https://docs.microsoft.com/azure/event-grid/)
- **CloudEvents Specification**: [https://cloudevents.io/](https://cloudevents.io/)
