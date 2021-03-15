ECHO off
CLS

ECHO CLEANING SOLUTION
dotnet clean AzureEventGridSimulator.sln -v q -c Release /nologo

ECHO REMOVE OUTPUT FOLDER "build-win-64"
rd /S /Q "build-win-64"

ECHO PUBLISHING SOLUTION
dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj -o "../build-win-64" -c Release -r win-x64

ECHO DONE!
pause
