
# Azure Event Grid Simulator

A simple Azure Event Grid simulator that provides topic endpoints that mimic the Event Grid functionality. This is useful for local testing purposes.

## Features


### Topics
You configure a topic and it's subscribers in `appsettings.json`. An example is shown below.
You can add multiple topics. One https port per topic.

```json
{
  "topics": [
    {
      "name": "mytopic1",
      "httpsPort": 6101,
      "Key": "TheLocal+DevelopmentKey=",
      "subscriptions": [
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
For each topic and https port that you configure you will need to map a certificate to the port.
You can do that using the following powershell script. It first finds your local (IIS Express) development certificate (you can use another cert if you wish). It then uses `netsh` to bind the certicate to the desired port.

```powershell
$localhostCertificate = Get-ChildItem -path cert:\LocalMachine\Root | `
                        where { $_.Subject -match "CN\=localhost" -and $_.notafter -ge (Get-Date)  } | `
                        Sort-Object -Descending -Property notafter | `
                        Select -First 1;

$thumbprint = $localhostCertificate.thumbprint;

&netsh.exe http add sslcert ipport=127.0.0.1:6101 certhash=$thumbprint appid="{9c959566-4d24-41f9-8ff5-b7236a886585}"

```

## Subscribers

You can configure 0 to _n_ subscribers to each topic. When a request is received for a topic, the incoming event will be forwarded to each of the subscribers with a `aeg-event-type=Notification` header. 

## Key Validation

If the incoming request contains either an `aeg-sas-token` or an `aeg-sas-key` header _and_ there is a `Key` configured for the topic then the simulator will validate the key and reject the request if the value in the header is not valid.
If you want to skip with validation then just

## Future Feature Ideas

- Subscriber retries
- Dead lettering
- 