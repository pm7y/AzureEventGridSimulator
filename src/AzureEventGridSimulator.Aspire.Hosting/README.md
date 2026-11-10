# AzureEventGridSimulator.Aspire.Hosting

Runs the [Azure Event Grid Simulator](https://github.com/pm7y/AzureEventGridSimulator) as a container from an Aspire AppHost, with topics and subscribers declared in code.

## Requirements

- A container runtime, such as Docker Desktop or Podman. The simulator runs from the [`pmcilreavy/azureeventgridsimulator`](https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator) image.
- A trusted developer certificate: `dotnet dev-certs https --trust`. Aspire hands it to the container, so there's no certificate file to export or mount.

## Usage

```csharp
var key = builder.AddParameter("orders-key", secret: true);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues = storage.AddQueues("queues");

var api = builder.AddProject<Projects.Api>("api");

var orders = builder
    .AddAzureEventGridSimulator("eventgrid")
    .AddTopic("orders", port: 60101, key: key)
    .WithWebhookSubscriber("api-webhook", api, path: "/api/events")
    .WithStorageQueueSubscriber("orders-queue", queues, queueName: "orders");

builder.AddProject<Projects.Publisher>("publisher").WithReference(orders).WaitFor(orders);
```

## What a referencing application gets

`WithReference(orders)` sets:

| Variable                  | Value                                                      |
| ------------------------- | ---------------------------------------------------------- |
| `ConnectionStrings__orders` | `Endpoint=https://localhost:60101/api/events;Key=<key>` |
| `ORDERS_URI`              | `https://localhost:60101/api/events`                       |
| `ORDERS_KEY`              | The key, when the topic has one                            |

A referencing container gets the simulator's address on the container network in place of `localhost`.

```csharp
var client = new EventGridPublisherClient(
    new Uri(configuration["ORDERS_URI"]!),
    new AzureKeyCredential(configuration["ORDERS_KEY"]!)
);
```

## Subscribers

| Method                          | Delivers to                              |
| ------------------------------- | ---------------------------------------- |
| `WithWebhookSubscriber`         | A webhook on another AppHost resource    |
| `WithStorageQueueSubscriber`    | An Azure Storage queue                   |
| `WithServiceBusQueueSubscriber` | A Service Bus queue                      |
| `WithServiceBusTopicSubscriber` | A Service Bus topic                      |
| `WithEventHubSubscriber`        | An Event Hub                             |

Webhook subscribers skip Event Grid's validation handshake by default, because the simulator starts validating before the target is ready. Pass `disableValidation: false` to keep it.

## Troubleshooting

- **A container publishing to a topic doesn't trust the simulator.** Aspire puts the developer certificate in each container and points `SSL_CERT_DIR` at it. Publishing from the host needs nothing extra, but a container client that ignores that variable has to be told, such as the `curlimages/curl` image with `curl --capath "$SSL_CERT_DIR"`.
- **A webhook or a container publisher fails certificate name validation.** Aspire addresses resources across the container boundary by names ending in `.dev.internal`, and your developer certificate may predate those names. Recreate it with `dotnet dev-certs https --clean`, then `dotnet dev-certs https --trust`.

## Limitations

- The simulator is for local development, so it's left out of Aspire's published manifest.
- Each topic's port is used both on the host and inside the container, so it must be free on the host.
- The image's tag matches this package's version. Call `.WithImageTag(...)` on the simulator to run another one.
- Azurite doesn't raise blob events, so uploading a blob won't publish anything to a topic. Publish a `Microsoft.Storage.BlobCreated` event to the topic yourself to test a handler.
