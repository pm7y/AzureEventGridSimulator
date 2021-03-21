ECHO off
CLS

ECHO CLEANING SOLUTION
dotnet clean AzureEventGridSimulator.sln -v q -c Release /nologo

ECHO REMOVE OUTPUT FOLDER "build-linux-64"
rd /S /Q "build-linux-64"

ECHO PUBLISHING SOLUTION
dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj -o "../build-linux-64" -c Release --self-contained true -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:TrimUnusedDependencies=true -r linux-x64

ECHO DONE!
pause
