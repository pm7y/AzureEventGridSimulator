# start with an sdk enabled ubuntu image so we can build source
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine as build
WORKDIR /source

# copy source
COPY /src/AzureEventGridSimulator .

# build source and publish as single file called 'AzureEventGridSimulator'
RUN dotnet publish -c release -o /artifact \
    -r alpine-x64 \
    -v q \
    --nologo \
    --self-contained true \
    -p:PublishReadyToRun=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimUnusedDependencies=true

# add binary artifact to new runtime-deps only image
FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine
WORKDIR /app

# add tzdata incase we want to set the timezone
RUN apk add --no-cache tzdata

# copy the binary only
COPY --from=build /artifact/AzureEventGridSimulator .

ENTRYPOINT ["./AzureEventGridSimulator"]
