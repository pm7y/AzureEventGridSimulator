ECHO off
CLS

ECHO CLEANING SOLUTION
dotnet clean AzureEventGridSimulator.sln -v q -c Release /nologo

ECHO REMOVE OUTPUT FOLDER "build-linux-64"
rd /S /Q "build-linux-64"

ECHO PUBLISHING SOLUTION
dotnet publish ./AzureEventGridSimulator/AzureEventGridSimulator.csproj -o "../build-linux-64" -v q -c Release --self-contained -f netcoreapp2.2 -r linux-x64 /nologo

ECHO DONE!
pause
