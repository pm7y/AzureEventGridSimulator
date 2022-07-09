# run a container from an existing image

$imageName="pmcilreavy/azureeventgridsimulator:latest";
$containerName="azureeventgridsimulator-offical";

if ($(docker ps --all --filter="name=$containerName") -like "*$containerName*") {
        # the container already exists to just (re)-start it
        docker restart $containerName
 }
 else {
        # create the container and run it
        docker run `
                --detach `
                --publish 60101:60101 `
                --name $containerName `
                -v ${pwd}/docker:/aegs `
                --platform linux/amd64 `
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
                $imageName
 }
