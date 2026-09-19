# start with an sdk enabled alpine image so we can build source
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

# copy build configuration files first
COPY /src/Directory.Build.props .
COPY /src/Directory.Packages.props .
COPY /src/.editorconfig .

# copy source
COPY /src/AzureEventGridSimulator ./AzureEventGridSimulator

ARG TARGETARCH
RUN rid="linux-musl-arm64" \
    && if [ "$TARGETARCH" = "amd64" ]; then rid="linux-musl-x64"; fi \
    && echo $rid > /tmp/rid

# build source and publish as single file called 'AzureEventGridSimulator'
# Note: CI=true skips the CSharpier and git-hook targets in Directory.Build.props, because the
#       tool manifest and .git aren't in the build context (the release zips and the tool also build with CI=true)
# Note: Trimming is disabled because the custom mediator (assembly scanning), Asp.Versioning, configuration binding and System.Text.Json rely on reflection
RUN dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj \
    -c release -o /artifact \
    -r "$(cat /tmp/rid)" \
    -v q \
    --nologo \
    --self-contained true \
    -p:PublishReadyToRun=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:CI=true

# add binary artifact to new runtime-deps only image
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
WORKDIR /app

# add tzdata incase we want to set the timezone
# hadolint ignore=DL3018
RUN apk add --no-cache tzdata

ENV ASPNETCORE_URLS=

# copy the binary only
COPY --from=build /artifact/AzureEventGridSimulator .

# run as the base image's non-root 'app' user (UID 1654, the .NET images' APP_UID), which owns
# /app and the default /app/dead-letters folder; bind mounts keep their host ownership and permissions.
# The UID is numeric so hosts and Kubernetes' runAsNonRoot can check it without resolving a name.
# Only the two folders are chowned: a recursive chown would copy the ~100 MB binary into another
# layer. The binary stays root-owned (0755), so the app user can run it but not write to it.
RUN mkdir -p /app/dead-letters && chown 1654 /app /app/dead-letters
USER 1654

ENTRYPOINT ["./AzureEventGridSimulator"]
