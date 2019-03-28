#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see http://aka.ms/containercompat 
FROM microsoft/nanoserver:sac2016 as certtool

FROM microsoft/dotnet:2.2-aspnetcore-runtime-nanoserver-1809 AS base
WORKDIR /app

FROM microsoft/dotnet:2.2-sdk-nanoserver-1809 AS build
WORKDIR /src
COPY ["AzureEventGridSimulator/AzureEventGridSimulator.csproj", "AzureEventGridSimulator/"]
RUN dotnet restore "AzureEventGridSimulator/AzureEventGridSimulator.csproj"
COPY . .
WORKDIR "/src/AzureEventGridSimulator"
RUN dotnet build "AzureEventGridSimulator.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "AzureEventGridSimulator.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .

COPY --from=build /src/root-ca.cer .
COPY --from=build /src/AzureEventGridSimulator/server.pfx .
COPY --from=certtool /Windows/System32/certoc.exe .
USER ContainerAdministrator
RUN certoc.exe -addstore root root-ca.cer
USER ContainerUser

EXPOSE 80
EXPOSE 5000

ENTRYPOINT ["dotnet", "AzureEventGridSimulator.dll"]
