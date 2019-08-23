dotnet clean AzureEventGridSimulator.sln
dotnet restore AzureEventGridSimulator.sln
dotnet build AzureEventGridSimulator.sln
dotnet test AzureEventGridSimulator.sln

rd /S /Q ..\..\build-linux-x64

dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj -o ..\..\build-linux-x64 -c Release --self-contained -f netcoreapp2.2 -r linux-x64

pause
