dotnet clean AzureEventGridSimulator.sln
dotnet restore AzureEventGridSimulator.sln
dotnet build AzureEventGridSimulator.sln
dotnet test AzureEventGridSimulator.sln

rd /S /Q ..\..\build-win64 

dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj -o ..\..\build-win64 -c Release --self-contained -f netcoreapp2.2 -r win-x64

pause
