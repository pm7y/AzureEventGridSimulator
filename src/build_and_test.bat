ECHO off
CLS

ECHO CLEANING SOLUTION
dotnet clean AzureEventGridSimulator.sln -v q -c Debug /nologo
dotnet clean AzureEventGridSimulator.sln -v q -c Release /nologo

ECHO RESTORING SOLUTION
dotnet restore AzureEventGridSimulator.sln -v q /nologo

ECHO BUILDING SOLUTION
dotnet build AzureEventGridSimulator.sln -v q -c Debug /nologo
dotnet build AzureEventGridSimulator.sln -v q -c Release /nologo

ECHO RUNNING TESTS
dotnet test AzureEventGridSimulator.sln -v q -c Debug --no-build --no-restore /nologo
dotnet test AzureEventGridSimulator.sln -v q -c Release --no-build --no-restore /nologo

ECHO DONE!
pause
