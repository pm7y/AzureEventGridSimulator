# start with an sdk enabled alpine image so we can build source
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-alpine as build
WORKDIR /source

# copy source
COPY /src/AzureEventGridSimulator .

# build source and publish as single file called 'AzureEventGridSimulator'
RUN dotnet publish -c release -o /artifact \
    #-r alpine-x64 \
    #-r osx.13-arm64 \
    -f net7.0 \
    -v q \
    --nologo \
    --self-contained true \
    -p:PublishReadyToRun=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true 
    #-p:TrimUnusedDependencies=true

# add binary artifact to new runtime-deps only image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine
WORKDIR /app

# add tzdata incase we want to set the timezone
RUN apk add --no-cache tzdata

ENV ASPNETCORE_URLS=

# copy the binary only
COPY --from=build /artifact/AzureEventGridSimulator .

ENTRYPOINT ["./AzureEventGridSimulator"]
