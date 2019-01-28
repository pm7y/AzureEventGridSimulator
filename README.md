
# Azure Event Grid Simulator

[![Build status](https://ci.appveyor.com/api/projects/status/7dhqhfg5lt73chsb?svg=true)](https://ci.appveyor.com/project/fallenidol/azureeventgridsimulator)

A simple simulator which provides endpoints that mimic the functionality of [Azure Event Grid](https://azure.microsoft.com/en-au/services/event-grid/) topics and subscribers and is compatible with the `Microsoft.Azure.EventGrid` client library. 

## Configuration
Topics and their subscribers are configured in the `appsettings.json` file.
You can add multiple topics. Each topic must have a unique port. Each topic can have multiple subscribers.
An example of one topic with one subscriber is shown below.

```json
{
  "topics": [
    {
      "name": "MyAwesomeTopic",
      "httpsPort": 60101,
      "key": "TheLocal+DevelopmentKey=",
      "subscribers": [
        {
          "name": "LocalAzureFunctionSubscription",
          "endpoint": "http://localhost:7071/runtime/webhooks/EventGrid?functionName=PersistEventToDb"
        }
      ]
    }
  ]
}
```

## Usage

Once configured and running, requests are `posted` to a topic endpoint. The endpoint of a topic will be in the form: `https://localhost:<configured-port>/api/events?api-version=2018-01-01`.

#### cURL Example

```bash
curl -k -H "Content-Type: application/json" -H "aeg-sas-key: TheLocal+DevelopmentKey=" -X POST "https://localhost:60101/api/events?api-version=2018-01-01" -d @Data.json
```
_Data.json_
```json
[{
  "id": "8727823",
  "subject": "/example/subject",
  "data": {
  	"MyProperty": "This is my awesome data!"
  },
  "eventType": "Example.DataType",
  "eventTime": "2019-01-01T00:00:00.000Z",
  "dataVersion": "1",
}]
```

#### Postman

An example request that you can import into [Postman](https://www.getpostman.com/) can be found in the AzureEventGridSimulator repo here https://github.com/pmcilreavy/AzureEventGridSimulator/blob/master/src/Azure%20Event%20Grid%20Simulator.postman_collection.json.

## Notes

### HTTPs

Azure Event Grid only accepts connections over https and so the simulator only supports _https_ too. The simulator uses the dotnet development certificate to secure each topic port. You can ensure that this certifcate is installed by running the following command.

``` dotnet dev-certs https```

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

|Field|Description|
|-----|-----------|
|Id|Must be a string. Not null or whitespace.|
|Subject|Must be a string. Not null or whitespace.|
|EventType|Must be a string. Not null or whitespace.|
|EventTime|Must be a valid date/time.|
|MetadataVersion|Must be null or `1`.|
|Topic|Leave null or empty. Event Grid will populate this field.|
|DataVersion|_Optional_. e.g. `1`.|
|Data|_Optional_. Any custom object.|

## Why?

There are a couple of similar projects out there. What I found though is that they don't adequately simulate an actual Event Grid Topic endpoint.

Azure Event Grid only excepts connections over https and the `Microsoft.Azure.EventGrid` client only sends requests over https. If you're posting events to an Event Grid topic using custom code then maybe this isn't an issue. If you are using the client library though then any test endpoint must be https.

Typically an event grid topic endpoint url is like so: _https://topic-name.location-name.eventgrid.azure.net/api/events_. Note that all the information needed to post to a topic is contained in the host part. The `Microsoft.Azure.EventGrid` client will essentially reduce the url you give it down to just the host part and prefix it with **https** (regardless of the original scheme). 

It posts the payload to https://host:port and drops the query uri. All of the existing simulator/ emulator projects I found don't support https and use a the query uri to distinguish between the topics. This isn't compatible with the `Microsoft.Azure.EventGrid` client.

## Future Development

- Subscription validation at start up. https://docs.microsoft.com/en-us/azure/event-grid/security-authentication
- Event filtering. https://docs.microsoft.com/en-us/azure/event-grid/event-filtering
- Subscriber retries & dead lettering. https://docs.microsoft.com/en-us/azure/event-grid/delivery-and-retry
