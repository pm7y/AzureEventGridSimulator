# Azure Event Grid Simulator

![GitHub Workflow Status](https://img.shields.io/github/workflow/status/pmcilreavy/AzureEventGridSimulator/ci)
![GitHub contributors](https://img.shields.io/github/contributors-anon/pmcilreavy/AzureEventGridSimulator)
![GitHub tag (latest SemVer)](https://img.shields.io/github/v/tag/pmcilreavy/AzureEventGridSimulator?label=latest)
![GitHub all releases](https://img.shields.io/github/downloads/pmcilreavy/AzureEventGridSimulator/total)
![Docker Pulls](https://img.shields.io/docker/pulls/pmcilreavy/azureeventgridsimulator)

A simulator that provides endpoints to mimic the functionality of [Azure Event Grid](https://azure.microsoft.com/en-au/services/event-grid/) topics and subscribers and is compatible with the `Microsoft.Azure.EventGrid` client library. NOTE: Currently only the `EventGrid` event schema is supported. Support for the `CloudEvent` schema may be added at a future date.

## Configuration

Topics and their subscribers are configured in the `appsettings.json` file.

You can add multiple topics. Each topic must have a unique port. Each topic can have multiple subscribers. Each topic can support EventGridEvents or CloudEvent schemas.
An example of one topic with support for an EventGridEvent schema and with one HTTP endpoint subscriber is shown below.

```json
{
  "topics": [
    {
      "name": "MyAwesomeTopic",
      "port": 60101,
      "key": "TheLocal+DevelopmentKey=",
      "type": "EventGridEvent",
      "subscribers": {
        "http": [
          {
            "name": "LocalAzureFunctionSubscription",
            "endpoint": "http://localhost:7071/runtime/webhooks/EventGrid?functionName=PersistEventToDb",
            "disableValidation": true
          }
        ]
      }
    }
  ]
}
```

### Topic Settings

- `name`: The name of the topic. It can only contain letters, numbers, and dashes.
- `port`: The port to use for the topic endpoint. The topic will listen on `https://0.0.0.0:{port}/`.
- `key`: The key that will be used to validate the `aeg-sas-key` or `aeg-sas-token` header in each request. If this is not supplied then no key validation will take place.
- `type`: The event schema for the topic. Both EventGridEvent (https://learn.microsoft.com/en-us/azure/event-grid/event-schema) and CloudSchema (https://learn.microsoft.com/en-us/azure/event-grid/cloud-event-schema) schemas are supported.
- `subscribers`: The subscriptions for this topic.

### Subscriber Settings

Two subscriber classifications are supported - HTTP endpoints and Azure Service Bus.

#### HTTP

- `name`: The name of the subscriber. It can only contain letters, numbers, and dashes.
- `endpoint`: The subscription endpoint url. Events received by topic will be sent to this address.
- `disableValidation`:
  - `false` (the default) subscription validation will be attempted each time the simulator starts.
  - `true` to disable subscription validation.

##### Subscription Validation

When a subscription is added to Azure Event Grid it first sends a validation event to the subscription endpoint. The validation event contains a `validationCode` which the subscription endpoint must echo back. If this does not occur then Azure Event Grid will not enable the subscription.

More information about subscription validation can be found at [https://docs.microsoft.com/en-us/azure/event-grid/webhook-event-delivery](https://docs.microsoft.com/en-us/azure/event-grid/webhook-event-delivery).

The Azure Event Grid Simualator will mimick this validation behaviour at start up but it can be disabled using the `disableValidation` setting (above).

#### Azure Service Bus

- `name`: The name of the subscriber. It can only contain letters, numbers, and dashes.
- `sharedAccessKeyName`: The shared access policy name.
- `sharedAccessKey`: The shared access policy key.
- `topic`: Destination topic/queue name.
- `disableValidation`:
  - `false` (the default) subscription validation will be attempted each time the simulator starts.
  - `true` to disable subscription validation.
- `properties`: Define headers that are included with the request sent to the destination.

##### Delivery Properties

Define headers that are included with the request sent to the destination.

- element: User property name.
- `type`: `dynamic` or `static`.
- `value`: JsonPath property selection.

Extending the example above to include two user properties (`message` and `eTag`). `message` will always have the value of `hello world` and `eTag` will use the value of the `eTag` property in the event's data payload.

```json
{
  "properties": [
    {
      "message": {
          "type": "static",
          "value" : "Hello world"
      },
      "eTag": {
          "type": "dynamic",
          "value": "data.eTag"
      }
    }
  ]
}
```

#### Filtering Events

Event filtering is configurable on each subscriber using the filter model defined here: https://docs.microsoft.com/en-us/azure/event-grid/event-filtering. This page provides a full guide to the configuration options available and all parts of this guide are currently supported. For ease of transition, explicit limitations have also been adhered to.
The restrictions mentioned have been further modified (https://azure.microsoft.com/en-us/updates/advanced-filtering-generally-available-in-event-grid/) and these new less restrictive filtering limits have been observed.

Extending the example above to include a basic filter which will only deliver events to the subscription if they are of a specific type is illustrated below.

```json
{
  "topics": [
    {
      "name": "MyAwesomeTopic",
      "port": 60101,
      "key": "TheLocal+DevelopmentKey=",
      "subscribers": 
        "http": [
          {
            "name": "LocalAzureFunctionSubscription",
            "endpoint": "http://localhost:7071/runtime/webhooks/EventGrid?functionName=PersistEventToDb",
            "filter": {
              "includedEventTypes": ["my.eventType"]
            }
          }
        ]
      }
    }
  ]
}
```

This can be extended to allow subject filtering:

```json
"filter": {
  "subjectBeginsWith": "/blobServices/default/containers/mycontainer/log",
  "subjectEndsWith": ".jpg",
  "isSubjectCaseSensitive": true
}
```

or advanced filtering:

```json
"filter": {
  "advancedFilters": [
    {
      "operatorType": "NumberGreaterThanOrEquals",
      "key": "Data.Key1",
      "value": 5
    },
    {
      "operatorType": "StringContains",
      "key": "Subject",
      "values": ["container1", "container2"]
    }
  ]
}
```

**Note:** you can also specify the configuration file to use by setting the `ConfigFile` command line argument, e.g.

```
AzureEventGridSimulator.exe --ConfigFile=/path/to/config.json
```

## Docker

There's a published image available on the [↗ Docker hub](https://hub.docker.com/r/pmcilreavy/azureeventgridsimulator) called `pmcilreavy/azureeventgridsimulator:latest`.
The image is not configured with any topics or subscribers. The configuration can be passed in via command line environment variables (as below) or via a json file.

### Docker Run

Here's an example of running a container based on that image and passing in the configuration via environment variables to create 1 topic with 2 subscribers.
In this example the folder `C:\src\AzureEventGridSimulator\docker` on the host is being shared with the container. **Note:** see the _notes_ section further below on how to create a certificate file.

```
docker run `
        --detach `
        --publish 60101:60101 `
        -v C:\src\AzureEventGridSimulator\docker:/aegs `
        -e ASPNETCORE_ENVIRONMENT=Development `
        -e ASPNETCORE_Kestrel__Certificates__Default__Path=/aegs/azureEventGridSimulator.pfx `
        -e ASPNETCORE_Kestrel__Certificates__Default__Password=Y0urSup3rCrypt1cPa55w0rd! `
        -e TZ=Australia/Brisbane `
        -e AEGS_Topics__0__name=ExampleTopic `
        -e AEGS_Topics__0__port=60101 `
        -e AEGS_Topics__0__key=TheLocal+DevelopmentKey= `
        -e AEGS_Topics__0__subscribers__0__name=RequestCatcherSubscription `
        -e AEGS_Topics__0__subscribers__0__endpoint=https://azureeventgridsimulator.requestcatcher.com/ `
        -e AEGS_Topics__0__subscribers__0__disableValidation=true `
        -e AEGS_Topics__0__subscribers__1__name=AzureFunctionSubscription `
        -e AEGS_Topics__0__subscribers__1__endpoint=http://host.docker.internal:7071/runtime/webhooks/EventGrid?functionName=ExampleFunction `
        -e AEGS_Topics__0__subscribers__1__disableValidation=true `
        -e AEGS_Serilog__MinimumLevel__Default=Verbose `
        pmcilreavy/azureeventgridsimulator:latest
```

### Docker Compose

There is a `docker-compose.yml` file in the src folder that you can use (or modify) to build your own Docker image.

```
docker-compose up   --build `
                    --force-recreate `
                    --remove-orphans `
                    --detach
```

## Using the Simulator

Once configured and running, requests are `posted` to a topic endpoint. The endpoint of a topic will be in the form: `https://localhost:<configured-port>/api/events?api-version=2018-01-01`.

#### cURL Example

```bash
curl -k -H "Content-Type: application/json" -H "aeg-sas-key: TheLocal+DevelopmentKey=" -X POST "https://localhost:60101/api/events?api-version=2018-01-01" -d @Data.json
```

_Data.json_

```json
[
  {
    "id": "8727823",
    "subject": "/example/subject",
    "data": {
      "MyProperty": "This is my awesome data!"
    },
    "eventType": "Example.DataType",
    "eventTime": "2019-01-01T00:00:00.000Z",
    "dataVersion": "1"
  }
]
```

#### Postman

An example request that you can import into [Postman](https://www.getpostman.com/) can be found in the AzureEventGridSimulator repo here https://github.com/pmcilreavy/AzureEventGridSimulator/blob/master/src/Azure%20Event%20Grid%20Simulator.postman_collection.json.

#### EventGridClient

```csharp
var client = new EventGridClient(new TopicCredentials("TheLocal+DevelopmentKey="));
await client.PublishEventsWithHttpMessagesAsync(
    topicHostname: "localhost:60101",
    events: new List<EventGridEvent> { <your event> });
```

## Notes

### HTTPs

Azure Event Grid only accepts connections over https and so the simulator only supports _https_ too.

The simulator will attempt to use the dotnet development certificate to secure each topic port.
You can ensure that this certificate is installed and trusted by running the following command.

`dotnet dev-certs https --trust`

You can also generate a certificate file (suitable for using with a Docker container) like so.

`dotnet dev-certs https --export-path ./docker/azureEventGridSimulator.pfx --password Y0urSup3rCrypt1cPa55w0rd!`

### Subscribers

A topic can have 0 to _n_ subscribers. When a request is received for a topic, the events will be forwarded to each of the subscribers with the addition of an `aeg-event-type: Notification` header. If the message contains multiple events, they will be sent to each subscriber one at a time inline with the Azure Event Grid behaviour. _"Event Grid sends the events to subscribers in an array that has a single event. This behavior may change in the future."_ https://docs.microsoft.com/en-us/azure/event-grid/event-schema

### Key Validation

The simulator supports both: `aeg-sas-key` or `aeg-sas-token` request headers. Using `aeg-sas-key` is the simplest way. Just set the value of the `aeg-sas-key` to the same `key` value configured for the topic. Using an `aeg-sas-token` is more secure as the `key` is hashed but it's a bit trickier to set up. More information on `sas token` can be found here https://docs.microsoft.com/en-us/azure/event-grid/security-authentication#sas-tokens.

If the incoming request contains either an `aeg-sas-token` or an `aeg-sas-key` header _and_ there is a `Key` configured for the topic then the simulator will validate the key and reject the request if the value in the header is not valid.
If you want to skip the validation then set the `Key` to _null_ in `appsettings.json`.

### Size Validation

Azure Event Grid imposes certain size limits to the overall message body and to the each individual event. The overall message body must be <= 1Mb and each individual event must be <= 64Kb. _These are the advertised size limits. My testing has shown that the actual limits are 1.5Mb and 65Kb._

### Message Validation

Ensures that the properties of each event meets the minimum requirements.

| Field           | Description                                               |
| --------------- | --------------------------------------------------------- |
| Id              | Must be a string. Not null or whitespace.                 |
| Subject         | Must be a string. Not null or whitespace.                 |
| EventType       | Must be a string. Not null or whitespace.                 |
| EventTime       | Must be a valid date/time.                                |
| MetadataVersion | Must be null or `1`.                                      |
| Topic           | Leave null or empty. Event Grid will populate this field. |
| DataVersion     | _Optional_. e.g. `1`.                                     |
| Data            | _Optional_. Any custom object.                            |

## Why?

There are a couple of similar projects out there. What I found though is that they don't adequately simulate an actual Event Grid Topic endpoint.

Azure Event Grid only excepts connections over https and the `Microsoft.Azure.EventGrid` client only sends requests over https. If you're posting events to an Event Grid topic using custom code then maybe this isn't an issue. If you are using the client library though then any test endpoint must be https.

Typically an event grid topic endpoint url is like so: _https://topic-name.location-name.eventgrid.azure.net/api/events_. Note that all the information needed to post to a topic is contained in the host part. The `Microsoft.Azure.EventGrid` client will essentially reduce the url you give it down to just the host part and prefix it with **https** (regardless of the original scheme).

It posts the payload to https://host:port and drops the query uri. All of the existing simulator/ emulator projects I found don't support https and use a the query uri to distinguish between the topics. This isn't compatible with the `Microsoft.Azure.EventGrid` client.

## Future Development

Some features that could be added if there was a need for them: -

- Subscriber retries & dead lettering. https://docs.microsoft.com/en-us/azure/event-grid/delivery-and-retry
- Certificate configuration in `appsettings.json`.
- Subscriber token auth
- Better Docker support.
- Maybe a web based console for admin stats etc.
