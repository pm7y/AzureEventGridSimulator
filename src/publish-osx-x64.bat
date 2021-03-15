ECHO off
CLS

ECHO CLEANING SOLUTION
dotnet clean AzureEventGridSimulator.sln -v q -c Release /nologo

ECHO REMOVE OUTPUT FOLDER "build-osx-64"
rd /S /Q "build-osx-64"

ECHO PUBLISHING SOLUTION
dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj -o "../build-osx-64" -c Release -r osx-x64

ECHO DONE!
pause
