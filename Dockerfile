# start with an sdk enabled alpine image so we can build source
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine as build
WORKDIR /source

# copy build configuration files first
COPY /src/Directory.Build.props .
COPY /src/Directory.Packages.props .

# copy source
COPY /src/AzureEventGridSimulator ./AzureEventGridSimulator

ARG TARGETARCH
RUN rid="linux-musl-arm64" \
    && if [ "$TARGETARCH" = "amd64" ]; then rid="linux-musl-x64"; fi \
    && echo $rid > /tmp/rid

# build source and publish as single file called 'AzureEventGridSimulator'
# Note: DesignTimeBuild=true skips the CSharpier formatting target
# Note: Trimming is disabled because MediatR and Asp.Versioning use reflection-based DI
RUN dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj \
    -c release -o /artifact \
    -r $(cat /tmp/rid) \
    -f net10.0 \
    -v q \
    --nologo \
    --self-contained true \
    -p:PublishReadyToRun=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:DesignTimeBuild=true

# add binary artifact to new runtime-deps only image
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
WORKDIR /app

# add tzdata incase we want to set the timezone
RUN apk add --no-cache tzdata

ENV ASPNETCORE_URLS=

# copy the binary only
COPY --from=build /artifact/AzureEventGridSimulator .

ENTRYPOINT ["./AzureEventGridSimulator"]
