# Note: powershell may throw misleading 'error' message - https://stackoverflow.com/questions/2095088/error-when-calling-3rd-party-executable-from-powershell-when-using-an-ide


docker run `
        -it `
        --detach `
        --publish 60101:60101 `
        -v C:\src\AzureEventGridSimulator\docker:/aegs `
        -e ASPNETCORE_ENVIRONMENT=Development `
        -e ASPNETCORE_Kestrel__Certificates__Default__Path=/aegs/azureEventGridSimulator.pfx `
        -e ASPNETCORE_Kestrel__Certificates__Default__Password=Y0urSup3rCrypt1cPa55w0rd! `
        -e AEGS_Topics__0__name=ExampleTopic `
        -e AEGS_Topics__0__port=60101 `
        -e AEGS_Topics__0__key=TheLocal+DevelopmentKey= `
        -e AEGS_Topics__0__subscribers__0__name=RequestCatcherSubscription `
        -e AEGS_Topics__0__subscribers__0__endpoint=https://azureeventgridsimulator.requestcatcher.com/ `
        -e AEGS_Topics__0__subscribers__0__disableValidation=true `
        --pull missing `
        fallenidol/azureeventgridsimulator:latest