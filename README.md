
# Azure Event Grid Simulator

[![Build status](https://ci.appveyor.com/api/projects/status/7dhqhfg5lt73chsb?svg=true)](https://ci.appveyor.com/project/fallenidol/azureeventgridsimulator)

A simple Azure Event Grid simulator that provides topic endpoints that mimic the Event Grid functionality. This is useful for local integration testing purposes.

## Features


### Topics
You configure a topic and it's subscribers in `appsettings.json`. An example is shown below.
You can add multiple topics. One https port per topic.

```json
{
  "topics": [
    {
      "name": "mytopic1",
      "httpsPort": 60101,
      "key": "TheLocal+DevelopmentKey=",
      "subscribers": [
        {
          "name": "FunctionSubscription",
          "endpoint": "http://localhost:7071/runtime/webhooks/EventGrid?functionName=PersistEventToDb"
        }
      ]
    }
  ]
}
```

## HTTPs

Azure Event Grid only accepts connections over https and so the simulator needs to do this too so that it can accept connections from the Azure SDK.
The simulator uses the dotnet development certificate. You can ensure that this is installed by running the following command.

``` dotnet dev-certs https```

## Subscribers

You can configure 0 to _n_ subscribers to each topic. When a request is received for a topic, the incoming event will be forwarded to each of the subscribers with a `aeg-event-type=Notification` header. 

## Key Validation

If the incoming request contains either an `aeg-sas-token` or an `aeg-sas-key` header _and_ there is a `Key` configured for the topic then the simulator will validate the key and reject the request if the value in the header is not valid.
If you want to skip the validation then set the `Key` to _null_ in `appsettings.json`.

## Size Validation

Azure Eveny Grid limits the overall array of events. It must be less than 1Mb and each individual event message must be less than 64Kb.

## Message Validation

Ensures that each message passes some minimal validation test.

## Future Feature Ideas

- Subscriber retries
- Dead lettering
- Subscription filtering
- Extensibility points
