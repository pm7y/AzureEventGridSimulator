dotnet clean AzureEventGridSimulator.sln
dotnet restore AzureEventGridSimulator.sln
dotnet build AzureEventGridSimulator.sln
dotnet test AzureEventGridSimulator.sln

rd /S /Q ..\..\osx-x64

dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj -o ..\..\build-osx-x64 -c Release --self-contained -f netcoreapp2.2 -r osx-x64

pause
